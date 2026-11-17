using System;
using System.Collections.Generic;

namespace CandyCruisers
{
    public enum EnemyColor { Red, Blue, Green, Purple, Yellow, Orange }

    public sealed class GridModel
    {
        public const int Columns = 6;
        public const int Rows = 11;
        public sealed class Occupant
        {
            public int Id { get; }
            public EnemyColor Color { get; internal set; }
            public int Column { get; internal set; }
            public int Row { get; internal set; }
            internal Occupant(int id, EnemyColor color, int column, int row)
            { Id = id; Color = color; Column = column; Row = row; }
        }

        private readonly Occupant[,] cells = new Occupant[Columns, Rows];
        private readonly Dictionary<int, Occupant> enemies = new Dictionary<int, Occupant>();
        private readonly Dictionary<int, int> imitationLinks = new Dictionary<int, int>();
        private readonly HashSet<int> specialImitations = new HashSet<int>();
        private readonly HashSet<int> completedImitations = new HashSet<int>();
        private readonly Dictionary<int, EnemyColor> copiedColors = new Dictionary<int, EnemyColor>();
        public EnemyColor? CopiedColor(int id) => copiedColors.TryGetValue(id, out var color) ? color : (EnemyColor?)null;
        public bool IsSpecialImitation(int id) => specialImitations.Contains(id);
        public bool IsAttached(int id) => completedImitations.Contains(id);
        public FleetState Fleet { get; }
        public GridModel() => Fleet = new FleetState(this);
        public int Count => enemies.Count;
        public Occupant Find(int id) => enemies.TryGetValue(id, out var enemy) ? enemy : null;
        public static bool InBounds(int column, int row) => column >= 0 && column < Columns && row >= 0 && row < Rows;
        public Occupant At(int column, int row) => InBounds(column, row) ? cells[column, row] : null;

        public bool OccupiedColumns(out int first, out int last)
        {
            first = Columns;
            last = -1;
            foreach (var enemy in enemies.Values)
            {
                first = Math.Min(first, enemy.Column);
                last = Math.Max(last, enemy.Column);
            }
            return last >= 0;
        }

        public bool TryShiftColumns(int offset)
        {
            foreach (var enemy in enemies.Values)
                if (!InBounds(enemy.Column + offset, enemy.Row)) return false;
            Array.Clear(cells, 0, cells.Length);
            foreach (var enemy in enemies.Values)
            {
                enemy.Column += offset;
                cells[enemy.Column, enemy.Row] = enemy;
            }
            return true;
        }

        public bool TryAdd(int id, EnemyColor color, int column, int row)
        {
            if (!InBounds(column, row) || cells[column, row] != null || enemies.ContainsKey(id) || !Enum.IsDefined(typeof(EnemyColor), color)) return false;
            if (color == EnemyColor.Orange && HasOrangeNeighbor(column, row)) return false;
            var enemy = new Occupant(id, color, column, row);
            enemies.Add(id, enemy);
            cells[column, row] = enemy;
            Fleet.Observe(color);
            return true;
        }

        public bool TryMove(int id, int column, int row)
        {
            if (!enemies.TryGetValue(id, out var enemy) || !InBounds(column, row)) return false;
            if (cells[column, row] == enemy) return true;
            if (cells[column, row] != null) return false;
            if (enemy.Color == EnemyColor.Orange && HasOrangeNeighbor(column, row, id)) return false;
            cells[enemy.Column, enemy.Row] = null;
            cells[column, row] = enemy;
            enemy.Column = column;
            enemy.Row = row;
            return true;
        }

        public bool Remove(int id)
        {
            if (!enemies.TryGetValue(id, out var enemy)) return false;
            RemoveImitationLinks(id);
            cells[enemy.Column, enemy.Row] = null;
            return enemies.Remove(id);
        }

        public bool HasOrangeNeighbor(int column, int row, int ignoredId = 0)
        {
            foreach (var neighbor in Neighbors(column, row))
                if (neighbor.Color == EnemyColor.Orange && neighbor.Id != ignoredId) return true;
            return false;
        }

        private EnemyColor AbilityColor(Occupant enemy) => IsAttached(enemy.Id) && TryGetImitationTarget(enemy.Id, out var host) ? host.Color : enemy.Color;

        public List<Occupant> OrangeSwapTargets(int id)
        {
            var result = new List<Occupant>();
            if (!enemies.TryGetValue(id, out var orange) || AbilityColor(orange) != EnemyColor.Orange) return result;
            foreach (var candidate in enemies.Values)
            {
                if (candidate.Color == EnemyColor.Orange || MatchingDepths(candidate.Id, candidate.Color).Count != 1 ||
                    HasOrangeNeighbor(candidate.Column, candidate.Row, id)) continue;
                foreach (var neighbor in Neighbors(orange.Column, orange.Row))
                    if (neighbor.Id != candidate.Id && neighbor.Color == candidate.Color)
                    { result.Add(candidate); break; }
            }
            return result;
        }

        public bool TryOrangeSwap(int id, int targetId)
        {
            var targets = OrangeSwapTargets(id);
            var target = targets.Find(candidate => candidate.Id == targetId);
            if (target == null) return false;
            var orange = enemies[id];
            int column = orange.Column, row = orange.Row;
            cells[column, row] = target;
            cells[target.Column, target.Row] = orange;
            orange.Column = target.Column; orange.Row = target.Row;
            target.Column = column; target.Row = row;
            return true;
        }

        public List<Occupant> ImitationTargets(int id)
        {
            var result = new List<Occupant>();
            var enemy = Find(id);
            if (enemy == null) return result;
            foreach (var neighbor in Neighbors(enemy.Column, enemy.Row))
                if (neighbor.Color != EnemyColor.Yellow || IsAttached(neighbor.Id) && HasHostConnection(neighbor.Id)) result.Add(neighbor);
            return result;
        }

        public bool BeginImitation(int id, int targetId, bool special = false)
        {
            var enemy = Find(id); var target = Find(targetId);
            if (enemy == null || target == null || enemy.Color != EnemyColor.Yellow || imitationLinks.ContainsKey(id) ||
                Math.Abs(enemy.Column - target.Column) + Math.Abs(enemy.Row - target.Row) != 1) return false;
            if (IsAttached(targetId) && !HasHostConnection(targetId)) return false;
            EnemyColor color = IsAttached(targetId) ? copiedColors[targetId] : target.Color;
            if (color == EnemyColor.Yellow) return false;
            int hostId = IsAttached(targetId) ? imitationLinks[targetId] : targetId;
            imitationLinks.Add(id, hostId);
            copiedColors.Add(id, color);
            if (special) { specialImitations.Add(id); completedImitations.Add(id); }
            return true;
        }

        public bool HasHostConnection(int id)
        {
            if (!IsAttached(id) || !TryGetImitationTarget(id, out var host) || !copiedColors.TryGetValue(id, out var color) || host.Color != color) return false;
            var seen = new HashSet<int> { id };
            var pending = new Queue<Occupant>();
            pending.Enqueue(Find(id));
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var neighbor in Neighbors(current.Column, current.Row))
                {
                    if (neighbor.Id == host.Id) return true;
                    if (neighbor.Color != color && (!IsAttached(neighbor.Id) || copiedColors[neighbor.Id] != color)) continue;
                    if (seen.Add(neighbor.Id)) pending.Enqueue(neighbor);
                }
            }
            return false;
        }

        // Left, right, up, down. Coverage is a union of half-sprites, not a blended tint.
        public int HostCoverage(int id)
        {
            var enemy = Find(id);
            if (enemy == null || !IsAttached(id)) return 0;
            EnemyColor color = copiedColors[id];
            int mask = 0;
            foreach (var neighbor in Neighbors(enemy.Column, enemy.Row))
            {
                EnemyColor shown = IsAttached(neighbor.Id) ? copiedColors[neighbor.Id] : neighbor.Color;
                if (shown != color) continue;
                mask |= neighbor.Column < enemy.Column ? 1 : neighbor.Column > enemy.Column ? 2 : neighbor.Row < enemy.Row ? 4 : 8;
            }
            return mask;
        }
        public bool ConvertIfCovered(int id)
        {
            int mask = HostCoverage(id);
            if (IsSpecialImitation(id) || (mask & 3) != 3 && (mask & 12) != 12) return false;
            var enemy = Find(id);
            EnemyColor color = copiedColors[id];
            CancelImitation(id);
            enemy.Color = color;
            Fleet.Observe(color);
            return true;
        }

        public bool TryGetImitationTarget(int id, out Occupant target)
        {
            target = null;
            return imitationLinks.TryGetValue(id, out int targetId) && enemies.TryGetValue(targetId, out target);
        }

        public void CancelImitation(int id)
        {
            imitationLinks.Remove(id);
            copiedColors.Remove(id);
            specialImitations.Remove(id);
            completedImitations.Remove(id);
        }

        public bool PromoteImitation(int id)
        {
            if (!imitationLinks.ContainsKey(id)) return false;
            specialImitations.Add(id);
            completedImitations.Add(id);
            return true;
        }

        public bool CompleteImitation(int id)
        {
            if (!TryGetImitationTarget(id, out var host)) return false;
            completedImitations.Add(id);
            return true;
        }

        private void RemoveImitationLinks(int id)
        {
            CancelImitation(id);
            foreach (var source in new List<int>(imitationLinks.Keys))
                if (imitationLinks[source] == id)
                {
                    if (specialImitations.Contains(source)) enemies[source].Color = EnemyColor.Yellow;
                    CancelImitation(source);
                }
        }

        public bool CanDescend
        {
            get
            {
                for (int column = 0; column < Columns; column++)
                    if (cells[column, Rows - 1] != null) return false;
                return true;
            }
        }

        public List<Occupant> RetreatStep()
        {
            var anchored = new HashSet<int>();
            var pending = new Queue<Occupant>();
            for (int column = 0; column < Columns; column++)
            {
                var enemy = cells[column, 0];
                if (enemy != null) { anchored.Add(enemy.Id); pending.Enqueue(enemy); }
            }
            while (pending.Count > 0)
            {
                var enemy = pending.Dequeue();
                foreach (var neighbor in Neighbors(enemy.Column, enemy.Row))
                    if (anchored.Add(neighbor.Id)) pending.Enqueue(neighbor);
            }

            // Use one connectivity snapshot so disconnected groups keep their shape.
            // Top-to-bottom movement vacates each destination before the next member moves.
            var moved = new List<Occupant>();
            for (int row = 1; row < Rows; row++)
            for (int column = 0; column < Columns; column++)
            {
                var enemy = cells[column, row];
                if (enemy == null || anchored.Contains(enemy.Id)) continue;
                cells[column, row - 1] = enemy;
                cells[column, row] = null;
                enemy.Row = row - 1;
                moved.Add(enemy);
            }
            return moved;
        }

        public bool TryDescend()
        {
            if (!CanDescend) return false;
            for (int row = Rows - 2; row >= 0; row--)
            for (int column = 0; column < Columns; column++)
            {
                var enemy = cells[column, row];
                cells[column, row + 1] = enemy;
                cells[column, row] = null;
                if (enemy != null) enemy.Row = row + 1;
            }
            return true;
        }

        public bool MatchesShot(int id, EnemyColor shotColor)
        {
            var enemy = Find(id);
            if (enemy == null) return false;
            return enemy.Color == shotColor || IsAttached(id) && copiedColors[id] == shotColor;
        }

        public bool RevertImitation(int id)
        {
            if (!imitationLinks.ContainsKey(id)) return false;
            CancelImitation(id);
            return true;
        }

        public int PromotionCount(List<Occupant> group)
        {
            if (group.Count == 0 || group[0].Color == EnemyColor.Yellow) return group.Count;
            var component = HostComponentDepths(group[0].Id);
            int hosts = 0;
            foreach (int member in component.Keys)
                if (Find(member).Color != EnemyColor.Yellow) hosts++;
            return hosts >= 2 ? component.Count : hosts;
        }

        // A copied-color junction can connect real hosts which do not touch each other.
        // Keep this separate from ColorGroup so Yellow identity and Yellow shot groups stay intact.
        private Dictionary<int, int> HostComponentDepths(int id)
        {
            var depths = new Dictionary<int, int>();
            var first = Find(id);
            if (first == null || first.Color == EnemyColor.Yellow && (!IsAttached(id) || !HasHostConnection(id))) return depths;
            EnemyColor color = IsAttached(id) ? copiedColors[id] : first.Color;
            var pending = new Queue<Occupant>();
            depths.Add(id, 1); pending.Enqueue(first);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var neighbor in Neighbors(current.Column, current.Row))
                {
                    if (depths.ContainsKey(neighbor.Id)) continue;
                    if (neighbor.Color != color && (!IsAttached(neighbor.Id) || copiedColors[neighbor.Id] != color || !HasHostConnection(neighbor.Id))) continue;
                    depths.Add(neighbor.Id, depths[current.Id] + 1);
                    pending.Enqueue(neighbor);
                }
            }
            return depths;
        }

        public List<Occupant> HostGroup(int id)
        {
            var result = new List<Occupant>();
            foreach (int member in HostComponentDepths(id).Keys)
                if (Find(member).Color != EnemyColor.Yellow) result.Add(Find(member));
            return result;
        }

        public Dictionary<int, int> MatchingDepths(int id, EnemyColor shotColor, bool magicMimic = false)
        {
            var depths = new Dictionary<int, int>();
            var first = Find(id);
            if (first == null) return depths;
            var pending = new Queue<Occupant>();
            void Visit(Occupant enemy, int depth)
            {
                if (depths.ContainsKey(enemy.Id)) return;
                depths.Add(enemy.Id, depth); pending.Enqueue(enemy);
            }
            if (magicMimic && IsAttached(id) && !IsSpecialImitation(id))
            {
                Visit(first, 1);
                foreach (var member in HostComponentDepths(id))
                    if (Find(member.Key).Color != EnemyColor.Yellow) Visit(Find(member.Key), member.Value);
            }
            else if (IsAttached(id) && shotColor != EnemyColor.Yellow)
            {
                if (IsSpecialImitation(id) || copiedColors[id] != shotColor) return depths;
                depths.Add(id, 1); // Deliberately never enqueue the mimic into yellow traversal.
                foreach (var member in HostComponentDepths(id))
                    if (Find(member.Key).Color != EnemyColor.Yellow) Visit(Find(member.Key), member.Value);
            }
            else
            {
                if (first.Color != shotColor) return depths;
                Visit(first, 1);
            }
            while (pending.Count > 0)
            {
                var enemy = pending.Dequeue();
                foreach (var neighbor in Neighbors(enemy.Column, enemy.Row))
                    if (neighbor.Color == enemy.Color) Visit(neighbor, depths[enemy.Id] + 1);
            }
            return depths;
        }

        public List<int> ClearMatchingChain(int id, EnemyColor shotColor, bool magicMimic = false)
        {
            var cleared = new List<int>(MatchingDepths(id, shotColor, magicMimic).Keys);
            // Discover the whole group before removal changes its connectivity.
            foreach (int clearedId in cleared) Remove(clearedId);
            return cleared;
        }

        public List<Occupant> ColorGroup(int id)
        {
            var result = new List<Occupant>();
            if (!enemies.TryGetValue(id, out var first)) return result;
            var seen = new HashSet<int> { id };
            var pending = new Queue<Occupant>();
            pending.Enqueue(first);
            while (pending.Count > 0)
            {
                var enemy = pending.Dequeue();
                result.Add(enemy);
                foreach (var neighbor in Neighbors(enemy.Column, enemy.Row))
                    if (neighbor.Color == first.Color && seen.Add(neighbor.Id)) pending.Enqueue(neighbor);
            }
            return result;
        }

        public bool SetColor(int id, EnemyColor color)
        {
            if (!enemies.TryGetValue(id, out var enemy) || !Enum.IsDefined(typeof(EnemyColor), color)) return false;
            if (color == EnemyColor.Orange && HasOrangeNeighbor(enemy.Column, enemy.Row, id)) return false;
            if (enemy.Color != color) RemoveImitationLinks(id);
            enemy.Color = color;
            Fleet.Observe(color);
            return true;
        }

        public int ColorCount(EnemyColor color)
        {
            int count = 0;
            foreach (var enemy in enemies.Values) if (enemy.Color == color) count++;
            return count;
        }

        public List<EnemyColor> AvailableColors()
        {
            var result = new List<EnemyColor>();
            foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                if (ColorCount(color) > 0) result.Add(color);
            return result;
        }

        public List<EnemyColor> SelectablePlayerColors() => AvailableColors();

        public IEnumerable<Occupant> Neighbors(int column, int row)
        {
            if (!InBounds(column, row)) yield break;
            var left = At(column - 1, row); if (left != null) yield return left;
            var right = At(column + 1, row); if (right != null) yield return right;
            var above = At(column, row - 1); if (above != null) yield return above;
            var below = At(column, row + 1); if (below != null) yield return below;
        }
    }
}
