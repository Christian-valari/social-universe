using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Safety
{
    // Persistent audio playback: BGM with crossfade (two ping-ponged AudioSources),
    // SFX as fire-and-forget one-shots. Volume is entirely the Settings panel's job
    // (AudioSettingsService drives AudioConfig.Mixer's Music/SFX groups) — this class
    // only decides *what* plays *when*, routed through those same groups so slider
    // changes apply immediately to whatever is already playing.
    public class AudioManager : IAudioManager
    {
        private readonly AudioCatalog _catalog;
        private readonly AudioSource  _bgmA;
        private readonly AudioSource  _bgmB;
        private readonly AudioSource  _sfx;
        private          AudioSource  _activeBgm;
        private readonly float        _crossfadeSeconds;
        private          int          _fadeGeneration;

        public AudioManager(AudioConfig config, AudioCatalog catalog)
        {
            _catalog = catalog;
            _crossfadeSeconds = config.CrossfadeSeconds;

            var root = new GameObject("AudioManager (runtime)");
            Object.DontDestroyOnLoad(root);

            AudioMixerGroup musicGroup = config.Mixer != null ? FindGroup(config.Mixer, "Music") : null;
            AudioMixerGroup sfxGroup   = config.Mixer != null ? FindGroup(config.Mixer, "SFX")   : null;

            _bgmA = CreateSource(root, "BgmA", musicGroup, loop: true);
            _bgmB = CreateSource(root, "BgmB", musicGroup, loop: true);
            _sfx  = CreateSource(root, "Sfx",  sfxGroup,   loop: false);
            _activeBgm = _bgmA;
        }

        public void PlaySfx(SfxId id)
        {
            var clip = _catalog.GetSfxClip(id);
            if (clip == null)
            {
                SULog.Warn($"AudioManager: no clip mapped for {id}", SULog.Channel.Core);
                return;
            }
            _sfx.PlayOneShot(clip);
        }

        public void PlayBgmForPlanet(PlanetDefinition planet) => CrossfadeTo(ResolvePlanetBgm(planet, _catalog));
        public void PlaySolarSystemBgm()                      => CrossfadeTo(_catalog.SolarSystemBgm);
        public void PlayTravelBgm()                           => CrossfadeTo(_catalog.TravelBgm);

        public static AudioClip ResolvePlanetBgm(PlanetDefinition planet, AudioCatalog catalog) =>
            planet != null && planet.BgmClip != null ? planet.BgmClip : catalog.FallbackPlanetBgm;

        private void CrossfadeTo(AudioClip clip)
        {
            if (clip == null || _activeBgm.clip == clip) return;

            var incoming = _activeBgm == _bgmA ? _bgmB : _bgmA;
            var outgoing = _activeBgm;

            incoming.clip   = clip;
            incoming.volume = 0f;
            incoming.Play();
            _activeBgm = incoming;

            int generation = ++_fadeGeneration;
            _ = FadeAsync(outgoing, incoming, generation, _crossfadeSeconds);
        }

        private async Task FadeAsync(AudioSource outgoing, AudioSource incoming, int generation, float durationSeconds)
        {
            float t = 0f;
            while (t < durationSeconds)
            {
                if (generation != _fadeGeneration) return; // a newer crossfade superseded this one
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / durationSeconds);
                outgoing.volume = 1f - p;
                incoming.volume = p;
                await Task.Yield();
            }
            if (generation != _fadeGeneration) return;
            outgoing.Stop();
            outgoing.volume = 1f;
            incoming.volume = 1f;
        }

        private static AudioSource CreateSource(GameObject root, string name, AudioMixerGroup group, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            var source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = group;
            source.loop = loop;
            source.playOnAwake = false;
            return source;
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
        {
            var groups = mixer.FindMatchingGroups(name);
            return groups.Length > 0 ? groups[0] : null;
        }
    }
}
