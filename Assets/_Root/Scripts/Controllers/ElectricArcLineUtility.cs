using UnityEngine;

namespace _Root.Scripts.Controllers
{
    /// <summary>
    /// LineRenderer üzerinde elektrik arkı benzeri segmentler oluşturur.
    /// </summary>
    public static class ElectricArcLineUtility
    {
        public struct Settings
        {
            public int SegmentsPerLine;
            public float WaveAmplitude;
            public float WaveFrequency;
            public float WaveScrollSpeed;
            public float SecondaryWaveAmplitude;
            public float SecondaryWaveFrequency;
            public float WidthPulseAmount;
            public float WidthPulseSpeed;

            public static Settings Default => new Settings
            {
                SegmentsPerLine = 18,
                WaveAmplitude = 0.28f,
                WaveFrequency = 14f,
                WaveScrollSpeed = 22f,
                SecondaryWaveAmplitude = 0.12f,
                SecondaryWaveFrequency = 31f,
                WidthPulseAmount = 0.08f,
                WidthPulseSpeed = 18f
            };
        }

        public static void PopulateElectricArc(
            LineRenderer line,
            LineRenderer widthTemplate,
            Vector3 start,
            Vector3 end,
            int segmentCount,
            float time,
            int phaseSeed,
            in Settings settings,
            Vector3[] segmentBuffer)
        {
            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.05f)
            {
                line.positionCount = 2;
                line.SetPosition(0, start);
                line.SetPosition(1, end);
                return;
            }

            Vector3 direction = delta / length;
            Vector3 perpendicular = Vector3.Cross(direction, Vector3.up);
            if (perpendicular.sqrMagnitude < 0.0001f)
                perpendicular = Vector3.Cross(direction, Vector3.right);
            perpendicular.Normalize();

            Vector3 perpendicular2 = Vector3.Cross(direction, perpendicular).normalized;
            float phaseOffset = phaseSeed * 0.173f;

            line.positionCount = segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                float t = i / (float)(segmentCount - 1);
                Vector3 basePoint = Vector3.Lerp(start, end, t);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float primary = Mathf.Sin(t * settings.WaveFrequency + time * settings.WaveScrollSpeed + phaseOffset)
                    * settings.WaveAmplitude;
                float secondary = Mathf.Sin(t * settings.SecondaryWaveFrequency - time * (settings.WaveScrollSpeed * 0.85f) + phaseOffset * 2f)
                    * settings.SecondaryWaveAmplitude;
                float offset = (primary + secondary) * envelope;

                Vector3 swirl = perpendicular * Mathf.Sin(time * 7f + t * 9f + phaseOffset)
                    + perpendicular2 * Mathf.Cos(time * 6f + t * 8f + phaseOffset);
                segmentBuffer[i] = basePoint + perpendicular * offset + swirl * (envelope * 0.15f);
            }

            line.SetPositions(segmentBuffer);

            if (widthTemplate != null)
            {
                float widthPulse = 1f + Mathf.Sin(time * settings.WidthPulseSpeed + phaseOffset) * settings.WidthPulseAmount;
                line.widthMultiplier = widthTemplate.widthMultiplier * widthPulse;
            }
        }

        public static void CopyLineRendererSettings(LineRenderer source, LineRenderer target)
        {
            target.useWorldSpace = true;
            target.loop = false;
            target.alignment = source.alignment;
            target.textureMode = source.textureMode;
            target.textureScale = source.textureScale;
            target.numCornerVertices = source.numCornerVertices;
            target.numCapVertices = source.numCapVertices;
            target.shadowBias = source.shadowBias;
            target.generateLightingData = source.generateLightingData;
            target.widthMultiplier = source.widthMultiplier;
            target.widthCurve = source.widthCurve;
            target.colorGradient = source.colorGradient;
            target.materials = source.materials;
        }
    }
}
