using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KartRush
{
    /// <summary>Top-down track map with a dot per racer (north = +Z). The track outline is drawn from the racing line.</summary>
    [RequireComponent(typeof(RawImage))]
    public class MiniMap : MonoBehaviour
    {
        public RaceManager race;
        public Vector2 size = new Vector2(360f, 240f);
        public float trackWidthMeters = 16f;

        RectTransform rect;
        float scale;              // UI units per world metre
        Vector2 centerXZ;
        readonly List<RectTransform> dots = new List<RectTransform>();
        readonly List<Racer> dotRacers = new List<Racer>();

        void Start()
        {
            rect = (RectTransform)transform;
            rect.sizeDelta = size;
            var pts = race.racingLine;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in pts)
            {
                min = Vector2.Min(min, new Vector2(p.x, p.z));
                max = Vector2.Max(max, new Vector2(p.x, p.z));
            }
            centerXZ = (min + max) * 0.5f;
            Vector2 span = max - min + Vector2.one * (trackWidthMeters * 1.5f);
            scale = Mathf.Min((size.x - 16f) / span.x, (size.y - 16f) / span.y);

            GetComponent<RawImage>().texture = BuildTexture(pts);
            GetComponent<RawImage>().raycastTarget = false;

            // Rivals first, the player last so it is drawn on top.
            foreach (var r in race.racers) if (!r.isPlayer) AddDot(r, 11f);
            foreach (var r in race.racers) if (r.isPlayer) AddDot(r, 16f);
        }

        Texture2D BuildTexture(Vector3[] pts)
        {
            int tw = Mathf.RoundToInt(size.x * 2f), th = Mathf.RoundToInt(size.y * 2f);
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[tw * th];
            var bg = new Color32(8, 12, 24, 150);
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            float texScale = scale * 2f;
            float roadR = Mathf.Max(3f, trackWidthMeters * 0.5f * texScale * 0.55f);
            DrawLoop(px, tw, th, pts, texScale, roadR + 2.5f, new Color32(255, 255, 255, 230));   // outline
            DrawLoop(px, tw, th, pts, texScale, roadR, new Color32(45, 48, 60, 255));              // road
            // start / finish tick
            Disc(px, tw, th, ToTex(pts[0], texScale, tw, th), 5f, new Color32(255, 220, 60, 255));

            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        Vector2 ToTex(Vector3 p, float texScale, int tw, int th) =>
            new Vector2(tw * 0.5f + (p.x - centerXZ.x) * texScale, th * 0.5f + (p.z - centerXZ.y) * texScale);

        void DrawLoop(Color32[] px, int tw, int th, Vector3[] pts, float texScale, float radius, Color32 c)
        {
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = ToTex(pts[i], texScale, tw, th), b = ToTex(pts[(i + 1) % pts.Length], texScale, tw, th);
                int steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / (radius * 0.5f)));
                for (int s = 0; s <= steps; s++) Disc(px, tw, th, Vector2.Lerp(a, b, s / (float)steps), radius, c);
            }
        }

        static void Disc(Color32[] px, int tw, int th, Vector2 c, float r, Color32 col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r)), x1 = Mathf.Min(tw - 1, Mathf.CeilToInt(c.x + r));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r)), y1 = Mathf.Min(th - 1, Mathf.CeilToInt(c.y + r));
            float r2 = r * r;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - c.x, dy = y - c.y;
                    if (dx * dx + dy * dy <= r2) px[y * tw + x] = col;
                }
        }

        void AddDot(Racer r, float dotSize)
        {
            var outline = new GameObject("Dot_" + r.displayName, typeof(RectTransform), typeof(Image));
            outline.transform.SetParent(transform, false);
            var ort = (RectTransform)outline.transform;
            ort.sizeDelta = Vector2.one * (dotSize + 4f);
            var oi = outline.GetComponent<Image>(); oi.color = r.isPlayer ? Color.white : new Color(0f, 0f, 0f, 0.85f); oi.raycastTarget = false;

            var inner = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(outline.transform, false);
            var irt = (RectTransform)inner.transform;
            irt.sizeDelta = Vector2.one * dotSize;
            var ii = inner.GetComponent<Image>(); ii.color = r.color; ii.raycastTarget = false;

            dots.Add(ort);
            dotRacers.Add(r);
        }

        void LateUpdate()
        {
            for (int i = 0; i < dots.Count; i++)
            {
                Vector3 p = dotRacers[i].transform.position;
                dots[i].anchoredPosition = new Vector2((p.x - centerXZ.x) * scale, (p.z - centerXZ.y) * scale);
            }
        }
    }
}
