using SDL;
using static SDL.SDL3;
using static SDL.SDL3_mixer;

namespace OsuStable.Native.Audio;

internal sealed unsafe class BeatmapAudioPlayer : IDisposable
{
    private MIX_Mixer* mixer;
    private MIX_Audio* audio;
    private MIX_Track* track;
    private bool mixerInitialized;

    public BeatmapAudioPlayer(string path, float gain = 1)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Beatmap audio was not found.", path);
        if (!MIX_Init())
            throw new InvalidOperationException($"SDL_mixer initialization failed: {SDL_GetError()}");

        mixerInitialized = true;
        try
        {
            mixer = MIX_CreateMixerDevice(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, null);
            if (mixer == null)
                throw new InvalidOperationException($"Audio device creation failed: {SDL_GetError()}");

            audio = MIX_LoadAudio(mixer, path, false);
            if (audio == null)
                throw new InvalidOperationException($"Beatmap audio load failed: {SDL_GetError()}");

            track = MIX_CreateTrack(mixer);
            if (track == null || !MIX_SetTrackAudio(track, audio))
                throw new InvalidOperationException($"Beatmap audio track creation failed: {SDL_GetError()}");
            MIX_SetTrackGain(track, Math.Clamp(gain, 0, 1));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Start()
    {
        if (!MIX_PlayTrack(track, 0))
            throw new InvalidOperationException($"Beatmap audio playback failed: {SDL_GetError()}");
    }

    public void Pause() => MIX_PauseTrack(track);
    public void Resume() => MIX_ResumeTrack(track);

    public double? PositionMilliseconds
    {
        get
        {
            var frames = MIX_GetTrackPlaybackPosition(track);
            if (frames < 0)
                return null;
            var milliseconds = MIX_TrackFramesToMS(track, frames);
            return milliseconds < 0 ? null : milliseconds;
        }
    }

    public void Dispose()
    {
        if (track != null)
        {
            MIX_DestroyTrack(track);
            track = null;
        }
        if (audio != null)
        {
            MIX_DestroyAudio(audio);
            audio = null;
        }
        if (mixer != null)
        {
            MIX_DestroyMixer(mixer);
            mixer = null;
        }
        if (mixerInitialized)
        {
            MIX_Quit();
            mixerInitialized = false;
        }
    }
}
