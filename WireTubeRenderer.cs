using UnityEngine;
using System;
using System.Collections.Generic;
using UnhollowerRuntimeLib;

namespace IEYTD2_SubmarineCode
{
    public class WireTubeRenderer : MonoBehaviour
    {
        public WireTubeRenderer(IntPtr ptr) : base(ptr) { }
        public WireTubeRenderer() : base(ClassInjector.DerivedConstructorPointer<WireTubeRenderer>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform[] nodes;

        public float radius = 0.02f;
        public int sides = 10;
        public float vTilesPerMeter = 6f;
        public int samplesPerSegment = 3;

        private Mesh _mesh;

        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Vector2[] _uvs;
        private int[] _triangles;

        private readonly List<Vector3> _pathPoints = new List<Vector3>(256);
        private float[] _cumulativeLength;

        private const float TwoPi = 6.28318548f;

        private void OnEnable()
        {
            if (GetComponent<MeshFilter>() == null) gameObject.AddComponent<MeshFilter>();
            if (GetComponent<MeshRenderer>() == null) gameObject.AddComponent<MeshRenderer>();
            EnsureMesh();
        }

        private void LateUpdate()
        {
            if (nodes == null || nodes.Length < 2)
            {
                if (_mesh != null) _mesh.Clear();
                return;
            }

            EnsureMesh();

            _pathPoints.Clear();
            BuildSampledPath(_pathPoints);

            int ringCount = _pathPoints.Count;
            if (ringCount < 2)
            {
                _mesh.Clear();
                return;
            }

            EnsureTopology(ringCount);
            FillGeometry(ringCount);

            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.uv = _uvs;
            _mesh.RecalculateBounds();
        }

        private void EnsureMesh()
        {
            var meshFilter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "WireTube" };
                _mesh.MarkDynamic();
                meshFilter.sharedMesh = _mesh;
            }
            else if (meshFilter.sharedMesh != _mesh)
            {
                meshFilter.sharedMesh = _mesh;
            }

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private void EnsureTopology(int ringCount)
        {
            int vertsPerRing = sides;
            int vertexCount = ringCount * vertsPerRing;

            int quadCount = (ringCount - 1) * vertsPerRing;
            int triangleIndexCount = quadCount * 6;

            if (_vertices == null || _vertices.Length != vertexCount)
            {
                _vertices = new Vector3[vertexCount];
                _normals = new Vector3[vertexCount];
                _uvs = new Vector2[vertexCount];
            }

            if (_triangles == null || _triangles.Length != triangleIndexCount)
            {
                _triangles = new int[triangleIndexCount];

                int tri = 0;
                for (int ring = 0; ring < ringCount - 1; ring++)
                {
                    int ringStart0 = ring * vertsPerRing;
                    int ringStart1 = (ring + 1) * vertsPerRing;

                    for (int side = 0; side < vertsPerRing; side++)
                    {
                        int nextSide = (side + 1) % vertsPerRing;

                        int a = ringStart0 + side;
                        int b = ringStart1 + side;
                        int c = ringStart1 + nextSide;
                        int d = ringStart0 + nextSide;

                        _triangles[tri++] = a; _triangles[tri++] = b; _triangles[tri++] = c;
                        _triangles[tri++] = a; _triangles[tri++] = c; _triangles[tri++] = d;
                    }
                }

                _mesh.Clear();
                _mesh.vertices = _vertices;
                _mesh.normals = _normals;
                _mesh.uv = _uvs;
                _mesh.triangles = _triangles;
            }

            if (_cumulativeLength == null || _cumulativeLength.Length != ringCount)
                _cumulativeLength = new float[ringCount];
        }

        private void FillGeometry(int ringCount)
        {
            _cumulativeLength[0] = 0f;
            for (int i = 1; i < ringCount; i++)
                _cumulativeLength[i] = _cumulativeLength[i - 1] + Vector3.Distance(_pathPoints[i - 1], _pathPoints[i]);

            Matrix4x4 toLocal = transform.worldToLocalMatrix;

            Vector3 upWorld = ChooseInitialUp(_pathPoints[0], _pathPoints[1]);
            Vector3 rightWorld = Vector3.right;

            for (int ring = 0; ring < ringCount; ring++)
            {
                Vector3 tangentWorld;
                if (ring == 0) tangentWorld = (_pathPoints[1] - _pathPoints[0]).normalized;
                else if (ring == ringCount - 1) tangentWorld = (_pathPoints[ring] - _pathPoints[ring - 1]).normalized;
                else tangentWorld = (_pathPoints[ring + 1] - _pathPoints[ring - 1]).normalized;

                TransportFrame(tangentWorld, ref upWorld, out rightWorld);

                Vector3 rightLocal = toLocal.MultiplyVector(rightWorld).normalized;
                Vector3 upLocal = toLocal.MultiplyVector(upWorld).normalized;

                Vector3 centerLocal = toLocal.MultiplyPoint(_pathPoints[ring]);

                int baseIndex = ring * sides;
                float v = _cumulativeLength[ring] * vTilesPerMeter;

                for (int side = 0; side < sides; side++)
                {
                    float angle = TwoPi * (side / (float)sides);
                    Vector3 radialLocal = (rightLocal * Mathf.Cos(angle) + upLocal * Mathf.Sin(angle)).normalized;

                    _vertices[baseIndex + side] = centerLocal + radialLocal * radius;
                    _normals[baseIndex + side] = radialLocal;
                    _uvs[baseIndex + side] = new Vector2(side / (float)sides, v);
                }
            }
        }

        private static Vector3 ChooseInitialUp(Vector3 p0, Vector3 p1)
        {
            Vector3 tangent = (p1 - p0).normalized;

            float x = Mathf.Abs(Vector3.Dot(Vector3.right, tangent));
            float y = Mathf.Abs(Vector3.Dot(Vector3.up, tangent));
            float z = Mathf.Abs(Vector3.Dot(Vector3.forward, tangent));

            if (x < y && x < z) return Vector3.right;
            if (y < z) return Vector3.up;
            return Vector3.forward;
        }

        private static void TransportFrame(Vector3 tangent, ref Vector3 up, out Vector3 right)
        {
            up -= Vector3.Project(up, tangent);
            up.Normalize();

            right = Vector3.Cross(tangent, up).normalized;
            up = Vector3.Cross(right, tangent).normalized;
        }

        private Vector3 NodePosition(int index)
        {
            if (index < 0) index = 0;
            if (index > nodes.Length - 1) index = nodes.Length - 1;
            return nodes[index].position;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u;
            float u3 = u2 * u;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * u +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * u3
            );
        }

        private void BuildSampledPath(List<Vector3> output)
        {
            output.Clear();
            output.Add(NodePosition(0));

            for (int segment = 0; segment < nodes.Length - 1; segment++)
            {
                Vector3 p0 = NodePosition(segment - 1);
                Vector3 p1 = NodePosition(segment);
                Vector3 p2 = NodePosition(segment + 1);
                Vector3 p3 = NodePosition(segment + 2);

                int steps = samplesPerSegment;
                for (int step = 1; step <= steps; step++)
                {
                    float u = step / (float)steps;
                    output.Add(CatmullRom(p0, p1, p2, p3, u));
                }
            }
        }
    }
}
