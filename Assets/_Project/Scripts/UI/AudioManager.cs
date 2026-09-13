using System;
using System.Collections.Generic;
using PokerDefense.Game;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * AudioManager
     *
     * 표현 계층에만 사는 사운드 재생기 - 게임 규칙은 오디오에 의존하지 않는다
     *
     * BGM은 준비 / 전투 / 보스 / 결과 상태에 맞춰 두 소스로 크로스페이드한다
     * SFX는 16개 보이스 풀에서 골라 재생하되, 앞쪽 8개는 전투음 전용으로 나눠
     * 발사음이 몰려도 알림음용 보이스는 남아 있게 한다
     */
    public sealed class AudioManager : MonoBehaviour
    {
        // 씬에 직렬화된 Cue 배열이 이 순서에 묶여 있으므로 값 순서를 바꾸지 않는다
        public enum Sfx
        {
            UiButton, CardDeal, ChipGain, UnitPlace, MergeStar2, MergeStar3,
            FireRapid, ImpactHit, EnemyDeath, HandHigh, LifeLoss, WaveClear,
            CardFlip, CardSelect, Exchange, HoldBonus, ShopOpen, UnitSummon,
            JokerPromote, FireHeavy, FireMulti, FireSplash, FirePierce,
            ImpactSplash, ImpactPierce, EnemyDeathBig, BossAppear, HandLow,
            HandMid, GameOver, StageClear, FireRoyalRifle, FirePistol
        }

        // 사운드 하나 - 변형 클립을 여러 개 두고 돌려 써서 반복감을 줄인다
        [Serializable]
        public sealed class Cue
        {
            public Sfx id;
            public AudioClip[] variants;
            [Range(0f, 1f)] public float gain = 0.4f;
            [Min(0f)] public float cooldown = 0.05f;
            [Range(0f, 0.1f)] public float pitchVariation = 0.025f;
        }

        public static AudioManager Instance { get; private set; }

        // 로열 라이플은 초당 발사 수가 많아 쿨다운을 따로 짧게 잡는다
        public const float RoyalRifleCooldown = 0.04f;

        [SerializeField] AudioSource musicSource;
        [SerializeField] AudioSource sfxSource;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.4f;
        [SerializeField, Range(0f, 1f)] float sfxVolume = 0.65f;
        [SerializeField] GameFlowController flow;
        [SerializeField] CombatController combat;
        [SerializeField] AudioClip bgmCombat;
        [SerializeField] AudioClip bgmPlan;
        [SerializeField] AudioClip bgmBoss;
        [SerializeField] AudioClip bgmResult;
        [SerializeField] Cue[] cues = Array.Empty<Cue>();

        // 앞 8개는 전투음 전용, 나머지는 그 외 사운드용
        const int CombatVoices = 8;
        const int TotalVoices = 16;

        readonly Dictionary<Sfx, Cue> lookup = new Dictionary<Sfx, Cue>();
        readonly Dictionary<Sfx, int> lastVariant = new Dictionary<Sfx, int>();
        readonly AudioCueGate gate = new AudioCueGate();
        readonly System.Random random = new System.Random();
        readonly List<Button> buttons = new List<Button>();
        readonly AudioSource[] voices = new AudioSource[TotalVoices];
        readonly float[] voiceGains = new float[TotalVoices];
        readonly double[] busyUntil = new double[TotalVoices];

        AudioSource musicB;
        float blend;
        float targetBlend;
        float duck = 1f;
        double duckUntil;
        bool bossWave;
        bool bossDeathPlayed;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (flow == null)
            {
                flow = FindAnyObjectByType<GameFlowController>();
            }

            if (combat == null)
            {
                combat = FindAnyObjectByType<CombatController>();
            }

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }

            musicB = gameObject.AddComponent<AudioSource>();
            Configure(musicSource, true);
            Configure(musicB, true);

            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = i == 0 ? sfxSource : gameObject.AddComponent<AudioSource>();
                Configure(voices[i], false);
                voices[i].priority = i < CombatVoices ? 160 : 64;
            }

            foreach (Cue cue in cues)
            {
                if (cue != null)
                {
                    lookup[cue.id] = cue;
                }
            }

            foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include))
            {
                // 카드에는 자체 클릭 피드백이 있으므로 공용 클릭음을 덧씌우지 않는다
                if (button.GetComponentInParent<CardView>() != null)
                {
                    continue;
                }

                buttons.Add(button);
                button.onClick.AddListener(PlayUiButton);
            }

            if (flow != null)
            {
                flow.HoldBonusEarned += OnHoldBonus;
                flow.ShopOpened += OnShopOpened;
            }

            if (combat != null)
            {
                combat.CombatStarted += OnCombatStarted;
                combat.CombatFinished += OnCombatFinished;
            }
        }

        static void Configure(AudioSource source, bool music)
        {
            source.Stop();
            source.playOnAwake = false;
            source.loop = music;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.pitch = 1f;
            source.volume = 0f;
            source.priority = music ? 32 : 128;
        }

        void Start()
        {
            musicSource.clip = bgmPlan != null ? bgmPlan : bgmCombat;

            if (musicSource.clip != null)
            {
                musicSource.Play();
            }
        }

        void LateUpdate()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i].volume = voiceGains[i] * AudioPreferences.EffectsGain;
            }

            // 결과 -> 드로우 전환과 상점 콜백이 모두 끝난 뒤의 최종 상태를 읽는다
            AudioClip desired;

            if (flow != null && flow.IsFinished)
            {
                desired = bgmResult;
            }
            else if (combat != null && combat.IsFighting)
            {
                desired = bossWave && bgmBoss != null ? bgmBoss : bgmCombat;
            }
            else
            {
                desired = bgmPlan;
            }

            SelectMusic(desired != null ? desired : bgmCombat);

            blend = Mathf.MoveTowards(blend, targetBlend, Time.unscaledDeltaTime / 0.8f);
            duck = Mathf.MoveTowards(duck, AudioSettings.dspTime < duckUntil ? 0.55f : 1f, Time.unscaledDeltaTime * 3f);

            // 두 소스가 같은 곡을 이어 재생하므로 볼륨 합이 1이 되게 섞어 가운데서 +3 dB 튀는 것을 막는다
            musicSource.volume = musicVolume * (1f - blend) * duck * AudioPreferences.MusicGain;
            musicB.volume = musicVolume * blend * duck * AudioPreferences.MusicGain;

            if (blend == 0f && targetBlend == 0f && musicB.isPlaying)
            {
                musicB.Stop();
            }

            if (blend == 1f && targetBlend == 1f && musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }

        void SelectMusic(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            // 이미 그 곡을 재생 중이면 해당 소스 쪽으로 블렌드만 되돌린다
            if (musicSource.clip == clip && musicSource.isPlaying)
            {
                targetBlend = 0;
                return;
            }

            if (musicB.clip == clip && musicB.isPlaying)
            {
                targetBlend = 1;
                return;
            }

            // 쉬고 있는 쪽 소스에 새 곡을 걸고, 재생 위치를 맞춘 뒤 그쪽으로 페이드한다
            AudioSource outgoing = blend <= 0.5f ? musicSource : musicB;
            AudioSource incoming = blend <= 0.5f ? musicB : musicSource;

            incoming.Stop();
            incoming.clip = clip;
            incoming.timeSamples = outgoing.clip == null ? 0 : outgoing.timeSamples % clip.samples;
            incoming.Play();

            targetBlend = incoming == musicB ? 1f : 0f;
        }

        void OnCombatStarted(CombatContext context)
        {
            bossWave = false;
            bossDeathPlayed = false;

            WaveDefinition wave = combat.Stage.CurrentWave;

            if (wave != null && wave.Entries != null)
            {
                foreach (WaveDefinition.SpawnEntry entry in wave.Entries)
                {
                    if (entry.count > 0 && entry.enemy != null && entry.enemy.Type == EnemyType.Boss)
                    {
                        bossWave = true;
                    }
                }
            }

            if (bossWave)
            {
                Play(Sfx.BossAppear);
            }
        }

        void OnCombatFinished(CombatOutcome outcome, int unresolved)
        {
            // 마지막 한 마리를 잡아 전투가 끝나면 CombatScreen이 뷰를 지우기 전에 여기서 보스 처치음을 낸다
            if (bossWave && bossDeathPlayed == false && outcome == CombatOutcome.Cleared)
            {
                Play(Sfx.EnemyDeathBig);
            }

            if (combat.Stage.IsGameOver)
            {
                Play(Sfx.GameOver);
            }
            else if (combat.Stage.IsAllWavesCleared)
            {
                Play(Sfx.StageClear);
            }
            else
            {
                Play(outcome == CombatOutcome.Cleared ? Sfx.WaveClear : Sfx.LifeLoss);
            }
        }

        void OnHoldBonus(int amount)
        {
            if (amount > 0)
            {
                Play(Sfx.HoldBonus);
            }
        }

        void OnShopOpened(IReadOnlyList<PokerDefense.Poker.Card> cards) => Play(Sfx.ShopOpen);

        void PlayUiButton() => Play(Sfx.UiButton);

        // 전투용 보이스로 재생해야 하는 사운드인지 - 알림음이 전투음에 밀리지 않게 풀을 나누는 기준
        public static bool IsCombatCue(Sfx id)
        {
            return id == Sfx.FireRapid || id == Sfx.FireHeavy || id == Sfx.FireMulti
                || id == Sfx.FireSplash || id == Sfx.FirePierce || id == Sfx.ImpactHit
                || id == Sfx.ImpactSplash || id == Sfx.ImpactPierce || id == Sfx.EnemyDeath
                || id == Sfx.FireRoyalRifle || id == Sfx.FirePistol;
        }

        // 유닛이 쏠 때 낼 발사음 - 지정 유닛은 전용 클립, 나머지는 공격 패턴별 클립
        public static Sfx FireCue(UnitDefinition unit)
        {
            if (unit != null && (unit.Id == "high_card" || unit.Id == "one_pair"))
            {
                return Sfx.FirePistol;
            }

            if (unit != null && unit.Id == "royal_straight_flush")
            {
                return Sfx.FireRoyalRifle;
            }

            return FireCue(unit != null ? unit.Pattern : AttackPattern.Rapid);
        }

        public static Sfx FireCue(AttackPattern pattern)
        {
            switch (pattern)
            {
                case AttackPattern.Heavy: return Sfx.FireHeavy;
                case AttackPattern.Multi: return Sfx.FireMulti;
                case AttackPattern.Splash: return Sfx.FireSplash;
                case AttackPattern.Pierce: return Sfx.FirePierce;
                default: return Sfx.FireRapid;
            }
        }

        // 쿨다운 -> 남는 보이스 -> 믹스 헤드룸을 차례로 확인하고, 여유가 있을 때만 클립을 하나 재생한다
        public void Play(Sfx id)
        {
            if (isActiveAndEnabled == false
                || lookup.TryGetValue(id, out Cue cue) == false
                || cue.variants == null || cue.variants.Length == 0)
            {
                return;
            }

            double now = AudioSettings.dspTime;

            if (gate.IsReady((int)id, now) == false)
            {
                return;
            }

            bool combatCue = IsCombatCue(id);
            int first = combatCue ? 0 : CombatVoices;
            int end = combatCue ? CombatVoices : TotalVoices;

            // 자기 풀에서 노는 보이스를 찾는다. 없으면(풀이 꽉 차면) 잘라내지 않고 그냥 버린다
            int slot = -1;

            for (int i = first; i < end; i++)
            {
                if (busyUntil[i] <= now)
                {
                    slot = i;
                    break;
                }
            }

            if (slot < 0)
            {
                return;
            }

            // 변형 클립을 고르되 직전에 쓴 것과 겹치면 다음 것으로 민다
            int variant = random.Next(cue.variants.Length);

            if (cue.variants.Length > 1 && lastVariant.TryGetValue(id, out int last) && variant == last)
            {
                variant = (variant + 1) % cue.variants.Length;
            }

            AudioClip clip = cue.variants[variant];

            if (clip == null)
            {
                return;
            }

            // 같은 풀에서 이미 울리고 있는 볼륨을 합쳐, 남은 헤드룸 안으로만 재생한다
            float usedGain = 0f;

            for (int i = first; i < end; i++)
            {
                if (busyUntil[i] > now)
                {
                    usedGain += voiceGains[i];
                }
            }

            float gain = Mathf.Min(sfxVolume * cue.gain, (combatCue ? 0.38f : 0.32f) - usedGain);

            if (gain < 0.01f)
            {
                return;
            }

            lastVariant[id] = variant;

            AudioSource source = voices[slot];
            source.clip = clip;
            source.pitch = 1f + (float)(random.NextDouble() * 2 - 1) * cue.pitchVariation;
            voiceGains[slot] = gain;
            source.volume = gain * AudioPreferences.EffectsGain;
            source.Play();

            if (id == Sfx.EnemyDeathBig)
            {
                bossDeathPlayed = true;
            }

            busyUntil[slot] = now + clip.length / source.pitch;
            gate.MarkPlayed((int)id, now, cue.cooldown);

            // 크고 드문 사운드가 나오는 동안에는 BGM을 잠깐 눌러(duck) 앞에 세운다
            if (id == Sfx.HandHigh || id == Sfx.BossAppear || id == Sfx.GameOver
                || id == Sfx.StageClear || id == Sfx.MergeStar3 || id == Sfx.JokerPromote)
            {
                duckUntil = Math.Max(duckUntil, now + Math.Min(clip.length, 1.5));
            }
        }

        void OnDisable()
        {
            if (Instance != this)
            {
                return;
            }

            foreach (AudioSource source in voices)
            {
                if (source != null)
                {
                    source.Stop();
                }
            }

            if (musicSource != null)
            {
                musicSource.Stop();
            }

            if (musicB != null)
            {
                musicB.Stop();
            }

            Array.Clear(busyUntil, 0, busyUntil.Length);
            gate.Clear();
        }

        void OnDestroy()
        {
            foreach (Button button in buttons)
            {
                if (button != null)
                {
                    button.onClick.RemoveListener(PlayUiButton);
                }
            }

            if (flow != null)
            {
                flow.HoldBonusEarned -= OnHoldBonus;
                flow.ShopOpened -= OnShopOpened;
            }

            if (combat != null)
            {
                combat.CombatStarted -= OnCombatStarted;
                combat.CombatFinished -= OnCombatFinished;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }

    /**
     * AudioCueGate
     *
     * 사운드별 쿨다운만 따지는 순수 로직
     * 프레임 레이트나 게임 타임스케일과 무관하도록 DSP 시간으로 잰다
     */
    public sealed class AudioCueGate
    {
        readonly Dictionary<int, double> next = new Dictionary<int, double>();

        public bool IsReady(int id, double now) => next.TryGetValue(id, out double at) == false || now >= at;

        public void MarkPlayed(int id, double now, double cooldown) => next[id] = now + Math.Max(0, cooldown);

        public void Clear() => next.Clear();
    }
}
