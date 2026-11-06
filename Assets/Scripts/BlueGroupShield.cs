using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    public sealed class BlueGroupShield : MonoBehaviour
    {
        private struct Edge
        {
            public Vector2Int From, To;
            public Vector3 A, B;
            public int Owner;
            public bool Traced;
        }
        private readonly List<Edge> edges = new List<Edge>();
        private readonly List<List<int>> contours = new List<List<int>>();
        private int contourCount;
        private Mesh band;
        private MeshRenderer bandRenderer;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private readonly HashSet<int> protectedIds = new HashSet<int>();
        public int EdgeCount => edges.Count;
        public int ContourCount => contourCount;
        public bool Protects(int id) => protectedIds.Contains(id);

        public void Refresh(EnemyGrid grid)
        {
            edges.Clear();
            contourCount = 0;
            protectedIds.Clear();
            var visited = new HashSet<int>();
            for (int row = 0; row < GridModel.Rows; row++)
            for (int column = 0; column < GridModel.Columns; column++)
            {
                var cell = grid.Model.At(column, row);
                if (cell == null || cell.Color != EnemyColor.Blue || !visited.Add(cell.Id)) continue;
                var group = grid.Model.ColorGroup(cell.Id);
                int source = 0;
                foreach (var member in group)
                {
                    visited.Add(member.Id);
                    var enemy = grid.View(member.Id);
                    var ability = enemy.GetComponent<EnemyAbilities>();
                    if (enemy.IsSpecial && ability != null && ability.isActiveAndEnabled && ability.ShieldActive) source = member.Id;
                }
                if (source == 0) continue;
                int firstEdge = edges.Count;
                foreach (var member in group)
                {
                    protectedIds.Add(member.Id);
                    var center = grid.CellPosition(member.Column, member.Row);
                    float half = grid.Spacing / 2;
                    var tl = center + new Vector3(-half, half, 0);
                    var tr = center + new Vector3(half, half, 0);
                    var bl = center + new Vector3(-half, -half, 0);
                    var br = center + new Vector3(half, -half, 0);
                    bool BlueAt(int x, int y) => grid.Model.At(x, y)?.Color == EnemyColor.Blue;
                    int cellColumn = member.Column, cellRow = member.Row;
                    if (!BlueAt(cellColumn, cellRow - 1)) AddEdge(new Vector2Int(cellColumn, cellRow), new Vector2Int(cellColumn + 1, cellRow), tl, tr, source);
                    if (!BlueAt(cellColumn + 1, cellRow)) AddEdge(new Vector2Int(cellColumn + 1, cellRow), new Vector2Int(cellColumn + 1, cellRow + 1), tr, br, source);
                    if (!BlueAt(cellColumn, cellRow + 1)) AddEdge(new Vector2Int(cellColumn + 1, cellRow + 1), new Vector2Int(cellColumn, cellRow + 1), br, bl, source);
                    if (!BlueAt(cellColumn - 1, cellRow)) AddEdge(new Vector2Int(cellColumn, cellRow + 1), new Vector2Int(cellColumn, cellRow), bl, tl, source);
                }
                TraceContours(firstEdge, edges.Count);
            }
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                enemy.GetComponent<EnemyAbilities>()?.SetGroupShielded(Protects(enemy.Id));
            if (band == null && edges.Count > 0)
            {
                var child = new GameObject("Rigid Blue perimeter", typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(transform, false);
                band = new Mesh { name = "Inward-fading Blue perimeter" };
                band.MarkDynamic();
                child.GetComponent<MeshFilter>().sharedMesh = band;
                bandRenderer = child.GetComponent<MeshRenderer>();
                bandRenderer.sharedMaterial = grid.View(edges[0].Owner).Visuals.Body.sharedMaterial;
                bandRenderer.sortingOrder = 14;
            }
            if (band == null) return;
            vertices.Clear(); colors.Clear(); triangles.Clear();
            for (int contourIndex = 0; contourIndex < contourCount; contourIndex++)
            {
                var contour = contours[contourIndex];
                int cornerCount = contour.Count;
                int firstVertex = vertices.Count;
                if (cornerCount < 4) continue;
                for (int strip = 0; strip <= 8; strip++)
                {
                    float t = strip / 8f;
                    float inset = Mathf.Lerp(-.0175f, .21f, t);
                    var tint = new Color(.82f, 1, 1, 1 - Mathf.SmoothStep(0, 1, t));
                    for (int corner = 0; corner < cornerCount; corner++)
                    {
                        var previous = edges[contour[(corner + cornerCount - 1) % cornerCount]];
                        var next = edges[contour[corner]];
                        Vector3 previousDirection = (previous.B - previous.A).normalized;
                        Vector3 nextDirection = (next.B - next.A).normalized;
                        vertices.Add(OffsetCorner(next.A, previousDirection, nextDirection, inset));
                        colors.Add(tint);
                    }
                }
                for (int strip = 0; strip < 8; strip++)
                {
                    int outer = firstVertex + strip * cornerCount;
                    int inner = outer + cornerCount;
                    for (int corner = 0; corner < cornerCount; corner++)
                    {
                        int nextCorner = (corner + 1) % cornerCount;
                        int a = outer + corner;
                        int b = outer + nextCorner;
                        int c = inner + corner;
                        int d = inner + nextCorner;
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(b); triangles.Add(d); triangles.Add(c);
                    }
                }
            }
            band.Clear();
            band.SetVertices(vertices); band.SetColors(colors); band.SetTriangles(triangles, 0);
            band.RecalculateBounds();
            bandRenderer.enabled = edges.Count > 0;
        }

        private void OnDestroy()
        {
            if (band == null) return;
            if (Application.isPlaying) Destroy(band); else DestroyImmediate(band);
        }

        private void AddEdge(Vector2Int from, Vector2Int to, Vector3 a, Vector3 b, int owner) =>
            edges.Add(new Edge { From = from, To = to, A = a, B = b, Owner = owner });

        private void TraceContours(int firstEdge, int endEdge)
        {
            for (int startEdge = firstEdge; startEdge < endEdge; startEdge++)
            {
                if (edges[startEdge].Traced) continue;
                var contour = contourCount < contours.Count ? contours[contourCount] : new List<int>();
                contour.Clear();
                Vector2Int start = edges[startEdge].From;
                int current = startEdge;
                bool closed = false;
                for (int remaining = endEdge - firstEdge + 1; remaining > 0; remaining--)
                {
                    var edge = edges[current];
                    if (edge.Traced) break;
                    edge.Traced = true;
                    edges[current] = edge;
                    contour.Add(current);
                    if (edge.To == start)
                    {
                        closed = true;
                        break;
                    }
                    current = FindNextEdge(edge.To, edge.To - edge.From, firstEdge, endEdge);
                    if (current < 0) break;
                }
                if (!closed || contour.Count < 4) continue;
                if (contourCount == contours.Count) contours.Add(contour);
                contourCount++;
            }
        }

        private int FindNextEdge(Vector2Int vertex, Vector2Int incoming, int firstEdge, int endEdge)
        {
            int selected = -1;
            int bestPriority = int.MaxValue;
            for (int i = firstEdge; i < endEdge; i++)
            {
                var candidate = edges[i];
                if (candidate.Traced || candidate.From != vertex) continue;
                int incomingDirection = DirectionIndex(incoming);
                int outgoingDirection = DirectionIndex(candidate.To - candidate.From);
                int turn = (outgoingDirection - incomingDirection + 4) % 4;
                int priority = turn == 1 ? 0 : turn == 0 ? 1 : turn == 3 ? 2 : 3;
                if (priority >= bestPriority) continue;
                bestPriority = priority;
                selected = i;
            }
            return selected;
        }

        private static int DirectionIndex(Vector2Int direction)
        {
            if (direction.x > 0) return 0;
            if (direction.y > 0) return 1;
            if (direction.x < 0) return 2;
            return 3;
        }

        private static Vector3 OffsetCorner(Vector3 point, Vector3 previous, Vector3 next, float inset)
        {
            var previousNormal = new Vector3(previous.y, -previous.x, 0);
            var nextNormal = new Vector3(next.y, -next.x, 0);
            var miter = previousNormal + nextNormal;
            if (miter.sqrMagnitude < .000001f) return point + nextNormal * inset;
            miter.Normalize();
            float scale = inset / Vector3.Dot(miter, nextNormal);
            return point + miter * scale;
        }

        public void AddShotLaneBoundaries(List<float> positions, float radius)
        {
            foreach (var edge in edges)
            {
                float a = transform.TransformPoint(edge.A).x, b = transform.TransformPoint(edge.B).x;
                positions.Add(Mathf.Min(a, b) - .0175f - radius + .00001f);
                positions.Add(Mathf.Max(a, b) + .0175f + radius - .00001f);
            }
        }

        public bool FindHit(Vector3 origin, float from, float to, float radius, out int id, out float distance)
        {
            id = 0;
            distance = float.PositiveInfinity;
            foreach (var edge in edges)
            {
                Vector3 a = transform.TransformPoint(edge.A), b = transform.TransformPoint(edge.B);
                if (origin.x + radius < Mathf.Min(a.x, b.x) - .0175f || origin.x - radius > Mathf.Max(a.x, b.x) + .0175f) continue;
                float near = Mathf.Min(a.y, b.y) - .0175f - origin.y;
                float far = Mathf.Max(a.y, b.y) + .0175f - origin.y;
                if (far < from || near > to) continue;
                float contact = Mathf.Max(from, near);
                if (contact >= distance) continue;
                id = edge.Owner;
                distance = contact;
            }
            return id != 0;
        }
    }
}
