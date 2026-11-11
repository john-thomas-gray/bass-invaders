using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    public sealed class EnemyGrid : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float spacing = 0.75f;
        private readonly AbilityBeatClock retreatClock = new AbilityBeatClock();
        private int lastRetreatBeat;
        [SerializeField] private GameObject missilePrefab;
        [SerializeField] private Sprite shieldSprite;
        public void ConfigureAbilities(GameObject missile, Sprite shield)
        { missilePrefab = missile; shieldSprite = shield; }
        public GridModel Model { get; } = new GridModel();
        private readonly Dictionary<int, GridEnemy> views = new Dictionary<int, GridEnemy>();
        public FleetState Fleet => Model.Fleet;
        public int SpecialGreenCount
        {
            get
            {
                int count = 0;
                foreach (var enemy in views.Values)
                    if (enemy != null && enemy.isActiveAndEnabled && enemy.IsSpecial && enemy.Color == EnemyColor.Green)
                        count++;
                return count;
            }
        }
        public int RegularGreenCount => Mathf.Max(0, Fleet.ColorCount(EnemyColor.Green) - SpecialGreenCount);
        public int ActiveGreenTempoCount
        {
            get
            {
                int count = 0;
                foreach (var enemy in views.Values)
                    if (enemy != null && enemy.isActiveAndEnabled &&
                        enemy.GetComponent<EnemyAbilities>()?.GreenTempoActive == true) count++;
                return count;
            }
        }
        private BlueGroupShield groupShield;
        private readonly List<PresentationCue> deaths = new List<PresentationCue>();
        public bool HasDeathEffects => deaths.Exists(effect => effect != null && !effect.Finished);
        public float Spacing => spacing;
        public BlueGroupShield GroupShield => groupShield;
        public List<EnemyColor> SeenColors() => Fleet.CopyColors();
        public bool IsColorCleared(EnemyColor color) => Fleet.IsCleared(color);
        public bool HasColorClearBar(EnemyColor color) => IsColorCleared(color);
        public bool AllColorClearBarsFilled => Fleet.AllColorsCleared;
        public void ResetColorClearStreak()
        {
            if (Fleet.ClearedColorCount == 0) return;
            var lostColors = Fleet.CopyColors().FindAll(Fleet.IsCleared);
            Fleet.ResetClearStreak();
            ColorClearStreakReset?.Invoke(lostColors);
        }
        public void BeginColorCycle() => Fleet.ResetCycle();
        public event System.Action FleetCleared;
        public event System.Action<EnemyColor> ColorCleared;
        public event System.Action<List<EnemyColor>> ColorClearStreakReset;
        public event System.Action<EnemyColor> EnemyDestroyed;
        public event System.Action LastYellowTransformed;
        public event System.Action<int, bool, int> MatchCleared;
        public event System.Action<EnemyColor> MatchColorScored;
        public event System.Action<int, bool, int> OrangeBurstCleared;
        public List<EnemyColor> SelectablePlayerColors(bool[] allowedColumns = null)
        {
            var choices = new List<EnemyColor>(PlayerColorWeights(allowedColumns).Keys);
            choices.Sort();
            return choices;
        }

        public Dictionary<EnemyColor, int> PlayerColorWeights(bool[] allowedColumns = null)
        {
            var weights = new Dictionary<EnemyColor, int>();
            var eligible = new HashSet<EnemyColor>();
            var occupiedRows = new HashSet<int>();
            var barrier = SpecialBlueBarrierRows();
            for (int column = 0; column < GridModel.Columns; column++)
                for (int row = 0; row < GridModel.Rows; row++)
                {
                    var enemy = Model.At(column, row);
                    if (enemy == null) continue;
                    occupiedRows.Add(row);
                    if (allowedColumns != null && (column >= allowedColumns.Length || !allowedColumns[column])) continue;
                    int weight = row + 1;
                    if (barrier != null && enemy.Color == EnemyColor.Blue) weight *= 2;
                    weights.TryGetValue(enemy.Color, out int previous);
                    weights[enemy.Color] = previous + weight;
                    if (enemy.Color == EnemyColor.Yellow && Model.TryGetImitationTarget(enemy.Id, out _)) continue;
                    if (enemy.Color != EnemyColor.Blue &&
                        (barrier != null && row <= barrier[column] || IsSpecialBlue(Model.At(column, row + 1)))) continue;
                    eligible.Add(enemy.Color);
                }
            foreach (var color in new List<EnemyColor>(weights.Keys))
                if (!eligible.Contains(color)) weights.Remove(color);
            bool boostGreen = occupiedRows.Count >= 6 && weights.ContainsKey(EnemyColor.Green) && Fleet.ColorCount(EnemyColor.Green) * 100 > Fleet.Count * 18;
            bool boostPurple = occupiedRows.Count >= 6 && weights.ContainsKey(EnemyColor.Purple) && Fleet.ColorCount(EnemyColor.Purple) * 100 > Fleet.Count * 18;
            if (boostGreen || boostPurple)
            {
                // Scale once so qualifying colors each receive exactly 1.75x their original weight.
                foreach (var color in new List<EnemyColor>(weights.Keys))
                    weights[color] *= color == EnemyColor.Green && boostGreen || color == EnemyColor.Purple && boostPurple ? 7 : 4;
            }
            return weights;
        }

        private int[] SpecialBlueBarrierRows()
        {
            if (!Model.OccupiedColumns(out int first, out int last)) return null;
            var visited = new HashSet<int>();
            var pending = new Queue<GridModel.Occupant>();
            int[] barrier = null;
            for (int column = first; column <= last; column++)
            for (int row = 0; row < GridModel.Rows; row++)
            {
                var start = Model.At(column, row);
                if (!IsSpecialBlue(start) || !visited.Add(start.Id)) continue;
                var component = new List<GridModel.Occupant>();
                bool touchesLeft = false, touchesRight = false;
                pending.Enqueue(start);
                while (pending.Count > 0)
                {
                    var enemy = pending.Dequeue();
                    component.Add(enemy);
                    touchesLeft |= enemy.Column == first;
                    touchesRight |= enemy.Column == last;
                    // Shield barriers connect at corners too; matching/promotion rules stay orthogonal.
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var neighbor = Model.At(enemy.Column + dx, enemy.Row + dy);
                        if (IsSpecialBlue(neighbor) && visited.Add(neighbor.Id)) pending.Enqueue(neighbor);
                    }
                }
                if (!touchesLeft || !touchesRight) continue;
                if (barrier == null)
                {
                    barrier = new int[GridModel.Columns];
                    for (int i = 0; i < barrier.Length; i++) barrier[i] = -1;
                }
                // The nearest spanning barrier controls each column, even with multiple walls.
                foreach (var enemy in component)
                    barrier[enemy.Column] = Mathf.Max(barrier[enemy.Column], enemy.Row);
            }
            return barrier;
        }
        private bool IsSpecialBlue(GridModel.Occupant enemy) => enemy != null &&
            enemy.Color == EnemyColor.Blue && View(enemy.Id) != null && View(enemy.Id).IsSpecial;
        public GridEnemy View(int id) => views.TryGetValue(id, out var enemy) ? enemy : null;
        private void Awake() => RegisterChildren();
        private void Update()
        {
            TickRetreat(Time.deltaTime);
            RefreshSpecials();
        }

        public void RefreshSpecials()
        {
            var visited = new HashSet<int>();
            foreach (var enemy in views.Values)
            {
                if (enemy == null || enemy.Color == EnemyColor.Orange || !visited.Add(enemy.Id)) continue;
                var group = Model.ColorGroup(enemy.Id);
                foreach (var member in group)
                {
                    visited.Add(member.Id);
                    var view = View(member.Id);
                    if (group.Count < 3 || view.IsSpecial) continue;
                    view.Promote();
                    view.GetComponent<EnemyAbilities>()?.OnPromoted();
                    if (view.GetComponent<EnemyAbilities>()?.IsTransforming == true) continue;
                    var spawner = GetComponent<EnemyRowSpawner>();
                    if (spawner != null) spawner.ApplyAppearance(view);
                    else view.Visuals.Body.sprite = EnemyPlaceholderArt.Triangle;
                }
            }
            if (groupShield == null) groupShield = gameObject.AddComponent<BlueGroupShield>();
            groupShield.Refresh(this);
        }

        private void OnDestroy()
        {
            foreach (var effect in deaths)
                if (effect != null)
                {
                    if (Application.isPlaying) Destroy(effect.gameObject);
                    else DestroyImmediate(effect.gameObject);
                }
        }

        public void TickRetreat(float seconds)
        {
            var music = GetComponent<GameplayMusicPlayer>();
            if (music != null && (music.PreviewTime.HasValue || Application.isPlaying && music.Source.clip != null))
                retreatClock.Advance(seconds, music.BeatPosition, music.Source.clip != null ? music.Source.clip.GetInstanceID() : 0);
            else retreatClock.Advance(seconds);
            int currentBeat = Mathf.FloorToInt(retreatClock.Position + .00001f);
            while (lastRetreatBeat < currentBeat)
            {
                lastRetreatBeat++;
                var moved = Model.RetreatStep();
                foreach (var cell in moved)
                {
                    if (!views.TryGetValue(cell.Id, out var enemy)) continue;
                    enemy.SetCell(cell.Column, cell.Row);
                    enemy.transform.localPosition = CellPosition(cell.Column, cell.Row);
                }
                if (moved.Count > 0) ExplodeTouchingOranges();
                if (moved.Count == 0)
                {
                    lastRetreatBeat = currentBeat;
                    break;
                }
            }
        }
        public void RegisterChildren()
        {
            foreach (var enemy in GetComponentsInChildren<GridEnemy>())
                if (enemy.isActiveAndEnabled) Register(enemy);
        }
        public Vector3 CellPosition(int column, int row) => new Vector3((column - (GridModel.Columns - 1) * .5f) * spacing, -row * spacing, 0);

        public bool OccupiedHorizontalBounds(out float left, out float right)
        {
            left = right = 0;
            if (!Model.OccupiedColumns(out int first, out int last)) return false;
            left = transform.TransformPoint(CellPosition(first, 0) + Vector3.left * spacing * 0.5f).x;
            right = transform.TransformPoint(CellPosition(last, 0) + Vector3.right * spacing * 0.5f).x;
            return true;
        }

        public bool AlignForNewRow() => AlignForNewRow(GridModel.Columns);
        public bool AlignForNewRow(int rowWidth)
        {
            rowWidth = Mathf.Clamp(rowWidth, 1, GridModel.Columns);
            float worldSpacing = Mathf.Abs(transform.TransformVector(Vector3.right * spacing).x);
            if (worldSpacing <= 0) return false;
            float left = transform.TransformPoint(CellPosition(0, 0) + Vector3.left * spacing * 0.5f).x;
            float right = transform.TransformPoint(CellPosition(rowWidth - 1, 0) + Vector3.right * spacing * 0.5f).x;
            float excess = left < -PlayerMovement.HalfWidth ? left + PlayerMovement.HalfWidth :
                right > PlayerMovement.HalfWidth ? right - PlayerMovement.HalfWidth : 0;
            int shift = Mathf.Abs(excess) <= 0.00001f ? 0 :
                (int)Mathf.Sign(excess) * Mathf.CeilToInt((Mathf.Abs(excess) - 0.00001f) / worldSpacing);
            if (shift == 0) return true;
            if (!Model.TryShiftColumns(shift)) return false;
            // Opposite origin/column shifts preserve every survivor's world position.
            transform.position -= Vector3.right * shift * worldSpacing;
            foreach (var enemy in views.Values)
            {
                var cell = Model.Find(enemy.Id);
                enemy.SetCell(cell.Column, cell.Row);
                enemy.transform.localPosition = CellPosition(enemy.Column, enemy.Row);
            }
            return true;
        }
        public bool TryDescend()
        {
            if (!Model.TryDescend()) return false;
            foreach (var enemy in views.Values)
            {
                var cell = Model.Find(enemy.Id);
                enemy.SetCell(cell.Column, cell.Row);
                enemy.transform.localPosition = CellPosition(enemy.Column, enemy.Row);
            }
            return true;
        }
        public bool Register(GridEnemy enemy)
        {
            if (views.ContainsKey(enemy.Id)) return true;
            if (!Model.TryAdd(enemy.Id, enemy.Color, enemy.Column, enemy.Row))
            {
                Debug.LogError("Invalid or occupied enemy cell: " + enemy.name, enemy);
                return false;
            }
            views.Add(enemy.Id, enemy);
            enemy.Visuals.Configure(enemy.Visuals.Body);
            enemy.Bind(this);
            enemy.transform.localPosition = CellPosition(enemy.Column, enemy.Row);
            if (missilePrefab != null && shieldSprite != null)
            {
                var abilities = enemy.GetComponent<EnemyAbilities>();
                if (abilities == null) abilities = enemy.gameObject.AddComponent<EnemyAbilities>();
                abilities.Configure(enemy, missilePrefab, shieldSprite);
            }
            return true;
        }
        public void Unregister(GridEnemy enemy)
        {
            Model.Remove(enemy.Id);
            views.Remove(enemy.Id);
            enemy.Bind(null);
        }
        public bool TryMove(int id, int column, int row)
        {
            if (!views.TryGetValue(id, out var enemy) || !Model.TryMove(id, column, row)) return false;
            enemy.SetCell(column, row);
            enemy.transform.localPosition = CellPosition(column, row);
            return true;
        }

        public bool TryOrangeSwap(int id)
        {
            var candidates = Model.OrangeSwapTargets(id);
            if (candidates.Count == 0) return false;
            var target = candidates[Random.Range(0, candidates.Count)];
            var orange = View(id);
            var other = View(target.Id);
            if (orange == null || other == null || !Model.TryOrangeSwap(id, target.Id)) return false;
            foreach (var enemy in new[] { orange, other })
            {
                var cell = Model.Find(enemy.Id);
                enemy.SetCell(cell.Column, cell.Row);
                enemy.transform.localPosition = CellPosition(cell.Column, cell.Row);
                enemy.GetComponent<EnemyPresentation>()?.PhaseIn(enemy.Color, EnemyColor.Orange);
            }
            RefreshSpecials();
            GetComponent<GameSession>()?.CheckPlayerContact();
            return true;
        }

        private void ExplodeTouchingOranges()
        {
            foreach (var enemy in new List<GridEnemy>(views.Values))
                if (enemy != null && View(enemy.Id) != null && enemy.Color == EnemyColor.Orange && Model.ColorGroup(enemy.Id).Count > 1)
                    ClearMatchingChain(enemy.Id, EnemyColor.Orange, null, true);
        }
        public bool SetColor(int id, EnemyColor color)
        {
            if (!views.TryGetValue(id, out var enemy) || !Model.SetColor(id, color)) return false;
            enemy.SetColor(color);
            var spawner = GetComponent<EnemyRowSpawner>();
            if (spawner != null) spawner.ApplyAppearance(enemy);
            return true;
        }

        // Return the depth added to the shot's running peak, not the number killed.
        public int ClearMagicChain(int id, int multiplierBefore)
        {
            var enemy = View(id);
            if (enemy == null) return 0;
            var depths = Model.MatchingDepths(id, enemy.Color);
            int addedDepth = 0;
            if (depths.Count >= 3)
                foreach (int depth in depths.Values) addedDepth = Mathf.Max(addedDepth, depth);
            ClearMatchingChain(id, enemy.Color, multiplierBefore);
            return addedDepth;
        }

        public int ClearMatchingChain(int id, EnemyColor color, int? magicOffset = null, bool orangeBurst = false)
        {
            var depths = Model.MatchingDepths(id, color);
            var cleared = Model.ClearMatchingChain(id, color);
            bool earnsMultipliers = cleared.Count >= 3;
            var multipliers = new Dictionary<int, int>(depths);
            if (orangeBurst)
                foreach (int clearedId in cleared) multipliers[clearedId] = 5;
            else if (!earnsMultipliers)
                foreach (int clearedId in cleared) multipliers[clearedId] = 1;
            else if (magicOffset.HasValue)
            {
                int offset = Mathf.Max(0, magicOffset.Value);
                foreach (int clearedId in cleared) multipliers[clearedId] = offset + depths[clearedId];
            }
            deaths.RemoveAll(effect => effect == null || effect.Finished);
            var removedColors = new HashSet<EnemyColor>();
            foreach (int clearedId in cleared)
            {
                if (!views.TryGetValue(clearedId, out var enemy)) continue;
                removedColors.Add(enemy.Color);
                if (enemy.Color == EnemyColor.Green && enemy.IsSpecial)
                    enemy.GetComponent<EnemyPresentation>()?.TempoDown();
                int animationDepth = depths[clearedId];
                var match = enemy.Visuals.Match(enemy.Color, animationDepth, multipliers[clearedId]);
                var defeat = enemy.Visuals.Defeat(enemy.Color, animationDepth, multipliers[clearedId], Fleet.ColorCount(enemy.Color) == 0);
                if (match != null) deaths.Add(match);
                if (defeat != null) deaths.Add(defeat);
                views.Remove(clearedId);
                enemy.Bind(null);
                EnemyDestroyed?.Invoke(enemy.Color);
                enemy.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(enemy.gameObject);
                else DestroyImmediate(enemy.gameObject);
            }
            if (cleared.Count > 0)
            {
                if (groupShield != null) groupShield.Refresh(this);
                foreach (EnemyColor removedColor in System.Enum.GetValues(typeof(EnemyColor)))
                    if (removedColors.Contains(removedColor) && Fleet.ColorCount(removedColor) == 0)
                    {
                        Fleet.MarkCleared(removedColor);
                        ColorCleared?.Invoke(removedColor);
                    }
                int scoreWeight = 0;
                foreach (int clearedId in cleared) scoreWeight += multipliers[clearedId];
                if (orangeBurst) OrangeBurstCleared?.Invoke(cleared.Count, Fleet.Count == 0, scoreWeight);
                else
                {
                    MatchCleared?.Invoke(cleared.Count, Fleet.Count == 0, scoreWeight);
                    // Score this contact first, then advance the combo before the next contact.
                    foreach (var removedColor in removedColors) MatchColorScored?.Invoke(removedColor);
                }
                if (Fleet.Count == 0) FleetCleared?.Invoke();
            }
            return cleared.Count;
        }

        public bool CompleteImitation(int id) => CompleteImitation(id, out _);

        public bool CompleteImitation(int id, out List<int> joinedGroup)
        {
            joinedGroup = new List<int>();
            if (!Model.TryGetImitationTarget(id, out var target)) return false;
            if (!SetColor(id, target.Color)) return false;
            foreach (var member in Model.ColorGroup(id)) joinedGroup.Add(member.Id);
            // Conversion is not a kill: no reward, clear bar, or spawn exclusion.
            if (Fleet.ColorCount(EnemyColor.Yellow) == 0) LastYellowTransformed?.Invoke();
            return true;
        }

        public void BeginYellowGroupPulse(IEnumerable<int> joinedGroup, float arrivalSeconds, float returnSeconds)
        {
            YellowGroupPulse.Ensure(this).Begin(this, joinedGroup, arrivalSeconds, returnSeconds);
        }

        public bool FindMatchingHit(Vector3 origin, float fromLength, float toLength,
            EnemyColor color, float radius, out int id, out float hitLength, bool magic = false)
        {
            id = 0;
            hitLength = float.PositiveInfinity;
            bool found = false;
            if (groupShield != null && !magic && color != EnemyColor.Blue)
            {
                groupShield.Refresh(this);
                found = groupShield.FindHit(origin, fromLength, toLength, radius, out id, out hitLength);
            }
            foreach (var pair in views)
            {
                var enemy = pair.Value;
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                var abilities = enemy.GetComponent<EnemyAbilities>();
                bool blocked = !magic && enemy.Color == EnemyColor.Blue && color != EnemyColor.Blue &&
                    abilities != null && abilities.isActiveAndEnabled && abilities.ShieldActive;
                if (blocked && groupShield != null && groupShield.Protects(enemy.Id)) continue;
                if (!magic && enemy.Color != color && !blocked &&
                    !(abilities != null && abilities.isActiveAndEnabled && abilities.IsDisguised)) continue;
                var bounds = blocked ? abilities.ShieldBounds : enemy.HitBounds;
                if (origin.x + radius < bounds.min.x || origin.x - radius > bounds.max.x) continue;
                float near = bounds.min.y - origin.y;
                float far = bounds.max.y - origin.y;
                if (far < fromLength || near > toLength) continue;
                float contact = Mathf.Max(fromLength, near);
                if (contact >= hitLength) continue;
                found = true;
                id = pair.Key;
                hitLength = contact;
            }
            return found;
        }

        public enum TongueHitResult { Stopped, Deflected, PassThrough, ShieldAbsorbed }

        public TongueHitResult ResolveTongueHit(int id, EnemyColor color, bool magic = false)
        {
            if (!views.TryGetValue(id, out var enemy)) return TongueHitResult.PassThrough;
            if (!magic && color != EnemyColor.Blue && groupShield != null)
            {
                groupShield.Refresh(this);
                if (groupShield.Protects(id)) return TongueHitResult.Deflected;
            }
            var abilities = enemy.GetComponent<EnemyAbilities>();
            if (!magic && abilities != null && abilities.isActiveAndEnabled && abilities.RevealDisguise(color)) return TongueHitResult.PassThrough;
            if (!magic && abilities != null && abilities.isActiveAndEnabled && abilities.Absorb(color))
                return enemy.IsSpecial ? TongueHitResult.Stopped : TongueHitResult.ShieldAbsorbed;
            if (magic) ClearMagicChain(id, 0);
            else ClearMatchingChain(id, color);
            return TongueHitResult.Stopped;
        }
    }
}
