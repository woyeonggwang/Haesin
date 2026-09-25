using UnityEngine;

/// <summary>
/// 위치에서 한 번 재생하고 사라지는 3D 효과음. AudioSource.PlayClipAtPoint 는 감쇠(로그, minDistance 1)를
/// 바꿀 수 없어 먼 대포 소리가 거의 안 들리므로 직접 만든다. Linear 감쇠: minDistance 까지 최대, maxDistance 에서 0.
/// </summary>
public static class OneShotAudio
{
    public static AudioSource Play(AudioClip clip, Vector3 position, float volume, float minDistance, float maxDistance, float pitch)
    {
        if (clip == null) return null;
        var go = new GameObject("OneShot_" + clip.name);
        go.transform.position = position;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        src.dopplerLevel = 0f;
        src.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
        return src;
    }
}
