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
     * 게임 진행에 맞춰 BGM과 효과음을 재생하는 클래스
     */
    public sealed class AudioManager : MonoBehaviour
    {
        // Sfx 순서를 바꾸면 안 됨
        // Cue.id는 씬에 int로 저장되므로 순서가 바뀌면 다른 사운드로 연결됨
        // AudioPolishSetup.CueFiles도 같은 순서를 따름
        public enum Sfx
        {
            UiButton, CardDeal, UnitPlace, MergeStar2, MergeStar3,
            FireRapid, ImpactHit, EnemyDeath, HandHigh, LifeLoss, WaveClear,
            CardFlip, CardSelect, Exchange, UnitSummon,
            JokerPromote, FireHeavy, FireMulti, FireSplash, FirePierce,
            ImpactSplash, ImpactPierce, EnemyDeathBig, BossAppear, HandLow,
            HandMid, GameOver, StageClear, FireRoyalRifle, FirePistol
        }

        // 사운드 하나를 담당하는 클래스
        // (변형 클립을 여러 개 두고 돌려 써서 반복감을 줄임)
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

        // 로열 라이플은 초당 발사 수가 많아 쿨타임을 따로 짧게 잡음
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

        // 앞 8개는 전투 사운드 전용, 나머지는 그 외 사운드
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
                // 카드에는 자체 클릭음이 있으므로 공용 클릭음을 덧씌우지 않음
                if (button.GetComponentInParent<CardView>() != null)
                {
                    continue;
                }

                buttons.Add(button);
                button.onClick.AddListener(PlayUiButton);
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
            // AudioPreferences 속성은 읽을 때마다 PlayerPrefs를 다시 읽으므로 루프 밖에서 한 번만 읽음
            float effectsGain = AudioPreferences.EffectsGain;

            for (int i = 0; i < voices.Length; i++)
            {
                voices[i].volume = voiceGains[i] * effectsGain;
            }

            // Update에서 바뀐 상태를 LateUpdate에서 읽어 이번 프레임의 BGM을 고름
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

            float musicGain = AudioPreferences.MusicGain;
            musicSource.volume = musicVolume * (1f - blend) * duck * musicGain;
            musicB.volume = musicVolume * blend * duck * musicGain;

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

            // 안 쓰는 소스에서 새 곡을 재생하고 그쪽으로 크로스페이드
            // 재생 위치는 이전 곡에서 이어받음
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
            // 보스가 마지막 적이면 CombatScreen이 처치음을 재생하지 못하므로 여기서 재생
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

        void PlayUiButton() => Play(Sfx.UiButton);

        // 전투용 보이스로 재생할 사운드인지 확인
        // 알림음이 전투음에 밀리지 않게 보이스를 나눔
        public static bool IsCombatCue(Sfx id)
        {
            return id == Sfx.FireRapid || id == Sfx.FireHeavy || id == Sfx.FireMulti
                || id == Sfx.FireSplash || id == Sfx.FirePierce || id == Sfx.ImpactHit
                || id == Sfx.ImpactSplash || id == Sfx.ImpactPierce || id == Sfx.EnemyDeath
                || id == Sfx.FireRoyalRifle || id == Sfx.FirePistol;
        }

        // 유닛의 발사음을 고름
        // 지정 유닛은 전용 발사음, 나머지는 공격 패턴별 발사음을 씀
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
                case AttackPattern.Heavy:
                    return Sfx.FireHeavy;
                case AttackPattern.Multi:
                    return Sfx.FireMulti;
                case AttackPattern.Splash:
                    return Sfx.FireSplash;
                case AttackPattern.Pierce:
                    return Sfx.FirePierce;
                default:
                    return Sfx.FireRapid;
            }
        }

        // 효과음을 재생함
        // 쿨다운 -> 남는 보이스 -> 남은 볼륨 순서로 확인해 하나라도 부족하면 재생하지 않음
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

            // 비어 있는 보이스를 찾음
            // 모두 재생 중이면 기존 소리를 끊지 않고 새 소리를 재생하지 않음
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

            // 변형 클립을 고르되 직전에 쓴 것과 겹치면 다음 것으로 넘김
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

            // 같은 풀에서 이미 울리고 있는 볼륨을 합쳐, 남은 볼륨 안으로만 재생함
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

            // 중요한 효과음이 잘 들리도록 BGM 볼륨을 잠깐 줄임 (최대 1.5초)
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
     * 사운드별 쿨타임만 따지는 클래스
     * 프레임 레이트나 timeScale과 무관하도록 오디오 시스템 시간으로 잼
     */
    public sealed class AudioCueGate
    {
        readonly Dictionary<int, double> next = new Dictionary<int, double>();

        public bool IsReady(int id, double now) => next.TryGetValue(id, out double at) == false || now >= at;

        public void MarkPlayed(int id, double now, double cooldown) => next[id] = now + Math.Max(0, cooldown);

        public void Clear() => next.Clear();
    }
}
