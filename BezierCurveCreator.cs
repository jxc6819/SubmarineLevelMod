using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public static class BezierCurveCreator
    {
        public static Vector3[] SampleCubic(
            Transform startSocket, Transform startControl, Transform endControl, Transform endSocket,
            int pointCount, bool approxEqualDistance = true)
        {
            if (startSocket == null || startControl == null || endControl == null || endSocket == null)
                return new Vector3[0];

            return SampleCubic(
                startSocket.position, startControl.position, endControl.position, endSocket.position,
                pointCount, approxEqualDistance);
        }

        public static Vector3[] SampleCubic(
            Vector3 start, Vector3 controlA, Vector3 controlB, Vector3 end,
            int pointCount, bool approxEqualDistance = true, int arcLutSamples = 128)
        {
            int count = Mathf.Max(2, pointCount);
            var points = new Vector3[count];

            if (!approxEqualDistance)
            {
                for (int i = 0; i < count; i++)
                {
                    float t01 = i / (float)(count - 1);
                    points[i] = CubicBezier(start, controlA, controlB, end, t01);
                }
                return points;
            }

            arcLutSamples = Mathf.Max(8, arcLutSamples);

            float[] cumulativeLength = new float[arcLutSamples + 1];
            cumulativeLength[0] = 0f;

            Vector3 lastPoint = CubicBezier(start, controlA, controlB, end, 0f);
            float totalLength = 0f;

            for (int i = 1; i <= arcLutSamples; i++)
            {
                float t01 = i / (float)arcLutSamples;
                Vector3 currentPoint = CubicBezier(start, controlA, controlB, end, t01);

                totalLength += Vector3.Distance(lastPoint, currentPoint);
                cumulativeLength[i] = totalLength;

                lastPoint = currentPoint;
            }

            if (totalLength <= 1e-8f)
            {
                for (int i = 0; i < count; i++)
                    points[i] = start;
                return points;
            }

            for (int i = 0; i < count; i++)
            {
                float fraction01 = i / (float)(count - 1);
                float targetLength = fraction01 * totalLength;

                int low = 0;
                int high = arcLutSamples;

                while (high - low > 1)
                {
                    int mid = (low + high) >> 1;
                    if (cumulativeLength[mid] < targetLength) low = mid;
                    else high = mid;
                }

                float segmentLength = cumulativeLength[high] - cumulativeLength[low];
                float segmentT0 = low / (float)arcLutSamples;
                float segmentT1 = high / (float)arcLutSamples;

                float t01;
                if (segmentLength <= 1e-8f)
                {
                    t01 = segmentT0;
                }
                else
                {
                    float segmentFraction01 = (targetLength - cumulativeLength[low]) / segmentLength;
                    t01 = Mathf.Lerp(segmentT0, segmentT1, segmentFraction01);
                }

                points[i] = CubicBezier(start, controlA, controlB, end, t01);
            }

            return points;
        }

        public static Vector3 CubicBezier(Vector3 start, Vector3 controlA, Vector3 controlB, Vector3 end, float t01)
        {
            t01 = Mathf.Clamp01(t01);

            float u = 1f - t01;
            float uu = u * u;
            float uuu = uu * u;

            float tt = t01 * t01;
            float ttt = tt * t01;

            return (uuu * start)
                 + (3f * uu * t01 * controlA)
                 + (3f * u * tt * controlB)
                 + (ttt * end);
        }

        public static Vector3 CubicBezierTangent(Vector3 start, Vector3 controlA, Vector3 controlB, Vector3 end, float t01)
        {
            t01 = Mathf.Clamp01(t01);

            float u = 1f - t01;

            return (3f * u * u) * (controlA - start)
                 + (6f * u * t01) * (controlB - controlA)
                 + (3f * t01 * t01) * (end - controlB);
        }
    }
}
