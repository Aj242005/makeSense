using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridSense.UI
{
    /// <summary>
    /// The degradation plot: measured wear per completed lap against the model's predicted curve,
    /// with the 95% band drawn at its true width.
    ///
    /// This is the one picture that shows the whole product thesis — a prediction, the thing it
    /// predicted, and an honest statement of how sure it was. It is drawn per lap rather than per
    /// tick because a degradation curve is a per-lap quantity; sampling it at 50 Hz would make a
    /// noisy line that says less.
    /// </summary>
    public class StintPlot : VisualElement
    {
        private readonly List<Vector2> measured = new List<Vector2>();   // x = lap, y = wear %
        private readonly List<Vector2> predicted = new List<Vector2>();
        private readonly List<Vector3> band = new List<Vector3>();       // x = lap, y = lower, z = upper

        private int totalLaps = 20;
        private bool hasModel;

        private const float PadLeft = 34f, PadBottom = 16f, PadTop = 8f, PadRight = 6f;

        private Label axisTop, axisMid, axisZero, axisLap1, axisLapN, legend;

        public StintPlot()
        {
            generateVisualContent += Paint;
            style.height = 132;

            // Labels are built once here, never during generateVisualContent: mutating the visual
            // tree inside mesh generation re-enters layout and can throw.
            axisTop = MakeLabel(true);
            axisMid = MakeLabel(true);
            axisZero = MakeLabel(true);
            axisLap1 = MakeLabel(false);
            axisLapN = MakeLabel(false);
            legend = MakeLabel(false);

            RegisterCallback<GeometryChangedEvent>(evt => LayoutLabels());
        }

        private Label MakeLabel(bool rightAlign)
        {
            Label l = new Label();
            l.style.position = Position.Absolute;
            l.style.fontSize = 9;
            l.style.color = Theme.InkDim;
            l.pickingMode = PickingMode.Ignore;
            if (rightAlign)
            {
                l.style.width = 28;
                l.style.unityTextAlign = TextAnchor.MiddleRight;
            }
            Add(l);
            return l;
        }

        public void SetStintLength(int laps)
        {
            if (laps == totalLaps) return;
            totalLaps = Mathf.Max(2, laps);
            LayoutLabels();
            MarkDirtyRepaint();
        }

        /// <summary>Records one completed lap. Called once per lap, not per frame.</summary>
        public void AddLap(int lap, float measuredWearPct, float predictedWearPct,
                           float lowerPct, float upperPct, bool modelLive)
        {
            measured.Add(new Vector2(lap, measuredWearPct));
            if (modelLive)
            {
                predicted.Add(new Vector2(lap, predictedWearPct));
                band.Add(new Vector3(lap, lowerPct, upperPct));
            }
            hasModel = modelLive;
            LayoutLabels();
            MarkDirtyRepaint();
        }

        public void Clear()
        {
            measured.Clear();
            predicted.Clear();
            band.Clear();
            LayoutLabels();
            MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ paint

        private void Paint(MeshGenerationContext ctx)
        {
            Rect r = contentRect;
            if (r.width < 8f || r.height < 8f) return;

            Painter2D p = ctx.painter2D;
            Rect plot = PlotRect(r);
            float maxWear = MaxWear();

            // --- grid: three horizontal rules with real values, and a baseline
            p.strokeColor = Theme.Rule;
            p.lineWidth = 1f;
            for (int i = 0; i <= 2; i++)
            {
                float f = i / 2f;
                float y = plot.yMax - f * plot.height;
                p.BeginPath();
                p.MoveTo(new Vector2(plot.x, y));
                p.LineTo(new Vector2(plot.xMax, y));
                p.Stroke();
            }

            // --- the band, at its true width
            if (band.Count >= 2)
            {
                p.fillColor = new Color(Theme.Energy.r, Theme.Energy.g, Theme.Energy.b, 0.16f);
                p.BeginPath();
                p.MoveTo(Project(band[0].x, band[0].z, plot, maxWear));
                for (int i = 1; i < band.Count; i++) p.LineTo(Project(band[i].x, band[i].z, plot, maxWear));
                for (int i = band.Count - 1; i >= 0; i--) p.LineTo(Project(band[i].x, band[i].y, plot, maxWear));
                p.ClosePath();
                p.Fill();
            }

            // --- the model's prediction
            if (predicted.Count >= 2)
            {
                p.strokeColor = Theme.Energy;
                p.lineWidth = 2f;
                p.BeginPath();
                p.MoveTo(Project(predicted[0].x, predicted[0].y, plot, maxWear));
                for (int i = 1; i < predicted.Count; i++)
                    p.LineTo(Project(predicted[i].x, predicted[i].y, plot, maxWear));
                p.Stroke();
            }

            // --- what actually happened
            if (measured.Count >= 2)
            {
                p.strokeColor = Theme.InkHi;
                p.lineWidth = 2f;
                p.BeginPath();
                p.MoveTo(Project(measured[0].x, measured[0].y, plot, maxWear));
                for (int i = 1; i < measured.Count; i++)
                    p.LineTo(Project(measured[i].x, measured[i].y, plot, maxWear));
                p.Stroke();
            }

            // --- lap markers on the measured line, so single-lap stints still read
            p.fillColor = Theme.InkHi;
            for (int i = 0; i < measured.Count; i++)
            {
                Vector2 v = Project(measured[i].x, measured[i].y, plot, maxWear);
                p.BeginPath();
                p.MoveTo(new Vector2(v.x - 2f, v.y - 2f));
                p.LineTo(new Vector2(v.x + 2f, v.y - 2f));
                p.LineTo(new Vector2(v.x + 2f, v.y + 2f));
                p.LineTo(new Vector2(v.x - 2f, v.y + 2f));
                p.ClosePath();
                p.Fill();
            }

        }

        private Rect PlotRect(Rect r)
        {
            return new Rect(r.x + PadLeft, r.y + PadTop,
                            Mathf.Max(1f, r.width - PadLeft - PadRight),
                            Mathf.Max(1f, r.height - PadTop - PadBottom));
        }

        private float MaxWear()
        {
            float m = 10f;
            for (int i = 0; i < measured.Count; i++) m = Mathf.Max(m, measured[i].y);
            for (int i = 0; i < band.Count; i++) m = Mathf.Max(m, band[i].z);
            return Mathf.Ceil(m * 1.15f / 5f) * 5f;
        }

        private Vector2 Project(float lap, float wear, Rect plot, float maxWear)
        {
            float fx = Mathf.Clamp01((lap - 1f) / Mathf.Max(1f, totalLaps - 1f));
            float fy = Mathf.Clamp01(wear / Mathf.Max(0.001f, maxWear));
            return new Vector2(plot.x + fx * plot.width, plot.yMax - fy * plot.height);
        }

        /// <summary>
        /// Positions the axis values. An unlabelled plot is decoration rather than a measurement,
        /// and Painter2D cannot draw text, so the numbers are real child labels.
        /// </summary>
        private void LayoutLabels()
        {
            if (axisTop == null) return;

            Rect r = contentRect;
            if (r.width < 8f || r.height < 8f) return;

            Rect plot = PlotRect(r);
            float maxWear = MaxWear();

            Place(axisTop, maxWear.ToString("F0") + "%", 2f, plot.y - 6f - r.y);
            Place(axisMid, (maxWear * 0.5f).ToString("F0") + "%", 2f, plot.center.y - 6f - r.y);
            Place(axisZero, "0%", 2f, plot.yMax - 6f - r.y);
            Place(axisLap1, "lap 1", plot.x - r.x, plot.yMax + 2f - r.y);
            Place(axisLapN, "lap " + totalLaps, plot.xMax - r.x - 34f, plot.yMax + 2f - r.y);
            Place(legend,
                hasModel ? "measured · predicted · 95% band" : "measured wear only",
                plot.x - r.x + 44f, plot.yMax + 2f - r.y);
        }

        private static void Place(Label l, string text, float left, float top)
        {
            l.text = text;
            l.style.left = left;
            l.style.top = top;
        }
    }
}
