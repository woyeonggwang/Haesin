using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;

// 플레이 중 성능 재기(MCP 에서 부름) — 프로파일러 카운터를 프레임마다 모아 평균·최대를 돌려줌.
// 정적 필드라 플레이 진입(도메인 리로드) 뒤에 Begin() 을 다시 불러야 함.
public static class PerfProbe
{
    struct Item { public string label; public ProfilerRecorder rec; public bool time; public bool bytes; }
    static readonly List<Item> items = new List<Item>();
    static int startFrame;

    static void Add(ProfilerCategory cat, string stat, string label, bool time = false, bool bytes = false)
    {
        items.Add(new Item { label = label, rec = ProfilerRecorder.StartNew(cat, stat, 600), time = time, bytes = bytes });
    }

    public static string Begin()
    {
        End();
        Add(ProfilerCategory.Render, "CPU Main Thread Frame Time", "CPU 메인(ms)", time: true);
        Add(ProfilerCategory.Render, "CPU Render Thread Frame Time", "CPU 렌더(ms)", time: true);
        Add(ProfilerCategory.Render, "GPU Frame Time", "GPU(ms)", time: true);
        Add(ProfilerCategory.Render, "CPU Total Frame Time", "CPU 전체(ms)", time: true);
        Add(ProfilerCategory.Render, "Triangles Count", "삼각형");
        Add(ProfilerCategory.Render, "Draw Calls Count", "드로우콜");
        Add(ProfilerCategory.Render, "Batches Count", "배치");
        Add(ProfilerCategory.Render, "SetPass Calls Count", "SetPass");
        Add(ProfilerCategory.Render, "Shadow Casters Count", "그림자 물체");
        Add(ProfilerCategory.Memory, "Gfx Used Memory", "그래픽 메모리(MB)", bytes: true);
        Add(ProfilerCategory.Render, "Used Textures Bytes", "텍스처(MB)", bytes: true);
        startFrame = Time.frameCount;
        return "started " + items.Count;
    }

    // 프로파일러 표시(마커) 시간도 재기 — "카테고리:이름" 을 쉼표로(예: "Internal:EditorLoop,Scripts:BehaviourUpdate"). Begin() 뒤에 부름.
    // 메인 스레드 시간이 어디에 드는지(에디터·스크립트·vSync 기다림 등) 보려고 넣음(2026-10-04).
    public static string AddMarkers(string csv)
    {
        int n = 0;
        foreach (var s in csv.Split(','))
        {
            var p = s.Split(':'); if (p.Length != 2) continue;
            Add(new ProfilerCategory(p[0].Trim()), p[1].Trim(), "[" + p[1].Trim() + "](ms)", time: true); n++;
        }
        return "markers " + n;
    }

    public static void Mark() { startFrame = Time.frameCount; }

    // 마지막 Mark 이후(최대 n 프레임) 평균·최대
    public static string Report(int n)
    {
        int since = Time.frameCount - startFrame;
        var sb = new StringBuilder();
        sb.Append("frames ").Append(since).Append(" res ").Append(Screen.width).Append('x').Append(Screen.height).Append('\n');
        foreach (var it in items)
        {
            int cnt = Mathf.Min(Mathf.Min(n, since), it.rec.Count);
            if (cnt <= 0) { sb.Append(it.label).Append(" -\n"); continue; }
            double sum = 0, mx = 0;
            for (int i = it.rec.Count - cnt; i < it.rec.Count; i++)
            {
                double v = it.rec.GetSample(i).Value;
                sum += v; if (v > mx) mx = v;
            }
            double avg = sum / cnt;
            double k = it.time ? 1e-6 : (it.bytes ? 1.0 / (1024 * 1024) : 1.0);
            sb.Append(it.label).Append(" 평균 ").Append((avg * k).ToString("F1")).Append(" 최대 ").Append((mx * k).ToString("F1")).Append('\n');
        }
        return sb.ToString();
    }

    public static void End()
    {
        foreach (var it in items) it.rec.Dispose();
        items.Clear();
    }
}
