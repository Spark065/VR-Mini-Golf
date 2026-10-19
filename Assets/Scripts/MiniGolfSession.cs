using System;
using System.Collections.Generic;

/// <summary>
/// Sahne nesnelerine bağlı olmayan, tek bir mini golf turunun çalışma zamanı verisi.
/// GameManager ile birlikte sahneler arasında yaşar; uygulama kapanınca diske yazılmaz.
/// </summary>
public sealed class MiniGolfSession
{
    private readonly Dictionary<int, int> completedHoleStrokes = new Dictionary<int, int>();

    public int CompletedHoleCount => completedHoleStrokes.Count;
    public int TotalStrokes { get; private set; }

    public void Reset()
    {
        completedHoleStrokes.Clear();
        TotalStrokes = 0;
    }

    public void RecordHole(int holeIndex, int strokeCount)
    {
        if (holeIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(holeIndex));
        }

        int safeStrokeCount = Math.Max(0, strokeCount);
        if (completedHoleStrokes.TryGetValue(holeIndex, out int previousStrokeCount))
        {
            TotalStrokes -= previousStrokeCount;
        }

        completedHoleStrokes[holeIndex] = safeStrokeCount;
        TotalStrokes += safeStrokeCount;
    }

    public bool TryGetHoleStrokes(int holeIndex, out int strokeCount)
    {
        return completedHoleStrokes.TryGetValue(holeIndex, out strokeCount);
    }

    public int GetFirstUnrecordedHoleIndex()
    {
        int holeIndex = 0;
        while (completedHoleStrokes.ContainsKey(holeIndex))
        {
            holeIndex++;
        }

        return holeIndex;
    }

    public int[] CreateHoleStrokeSnapshot(int holeCount)
    {
        int safeHoleCount = Math.Max(0, holeCount);
        int[] snapshot = new int[safeHoleCount];
        Array.Fill(snapshot, -1);

        foreach (KeyValuePair<int, int> completedHole in completedHoleStrokes)
        {
            if (completedHole.Key >= 0 && completedHole.Key < snapshot.Length)
            {
                snapshot[completedHole.Key] = completedHole.Value;
            }
        }

        return snapshot;
    }
}
