// ============================================================================
// HECTON-8 — PowerNode.cs
// Komponent energopodklyucheniya na kazhdom module bazy.
//
// OTVETSTVENNOSTI:
//   1. Pri spavne — chtenie bazovogo potrebleniya iz BuildableData.
//   2. Pri spavne — poisk sosednih PowerNode (OverlapSphereNonAlloc).
//   3. Sozdanie/vstuplenie v PowerGrid (ili obedinenie setey).
//   4. Sbor vseh IPowerComponent na svoem GameObject.
//   5. Pri despavne — vyhod iz PowerGrid (s proverkoy svyaznosti).
//   6. Realizatsiya IPowerComponent dlya bazovogo potrebleniya modulya.
//
// BAZOVOE POTREBLENIE (Data-Driven):
//   PowerNode sam realizuet IPowerComponent.
//   Pri OnSpawn() chitaet BuildableData cherez ModuleMarker:
//     BuildableData.powerRating → PowerRating (polozhit. ili otritsat.)
//     BuildableData.powerPriority → PowerPriority
//
//   Eto BAZOVOE potreblenie modulya (steny, osveschenie, ventilyatsiya).
//   Dopolnitelnye potrebiteli (Fabricator, LifeSupport) dobavlyayut
//   svoi IPowerComponent poverh bazovogo.
//
// ARHITEKTURA:
//   • IPoolable — korrektnaya rabota s ObjectPoolManager.
//   • IPowerComponent — bazovoe potreblenie iz BuildableData.
//   • OverlapSphereNonAlloc — zero GC poisk sosedey.
//   • _components — kesh vseh IPowerComponent na etom obekte.
//   • _neighbors — kesh sosednih PowerNode (pryamye svyazi).
//
// ZERO GC:
//   • Static Collider[] bufer dlya OverlapSphere — odna allokatsiya.
//   • List<IPowerComponent> zapolnyaetsya GetComponents — zero GC.
//   • List<PowerNode> _neighbors — pre-allocated.
//   • ReferenceEquals dlya proverki dublikatov — zero GC.
//
// NASTROYKA PREFABA:
//   1. Povesit PowerNode na finalPrefab modulya bazy.
//   2. Ustanovit connectionRadius (chut bolshe snap-setki).
//   3. ModuleMarker dolzhen byt nastroen s BuildableData.
//   4. BuildableData dolzhna soderzhat powerRating i powerPriority.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Power
{
    [DisallowMultipleComponent]
    public sealed class PowerNode : MonoBehaviour, IPoolable, IPowerComponent
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Connection ────────────────────────────────")]
        [Tooltip("Radius poiska sosednih moduley (metry). " +
                 "Dolzhen byt chut bolshe razmera snap-setki. " +
                 "Rekomendatsiya: razmer modulya × 1.1")]
        [SerializeField] private float connectionRadius = 5f;

        [Tooltip("Sloi, na kotoryh ischutsya sosednie PowerNode.")]
        [SerializeField] private LayerMask connectionMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Header("── Fallback (esli net ModuleMarker) ──────────")]
        [Tooltip("Bazovoe potreblenie esli ModuleMarker otsutstvuet. " +
                 "Otritsatelnoe = potreblyaet, polozhitelnoe = generiruet.")]
        [SerializeField] private float fallbackPowerRating;

        [Tooltip("Prioritet otklyucheniya esli ModuleMarker otsutstvuet.")]
        [Range(0, 100)]
        [SerializeField] private int fallbackPowerPriority = 50;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Set, k kotoroy prinadlezhit etot uzel.</summary>
        private PowerGrid _grid;

        /// <summary>
        /// Kesh vseh IPowerComponent na etom GameObject.
        /// Vklyuchaet sam PowerNode (on tozhe IPowerComponent).
        /// Zapolnyaetsya pri OnSpawn cherez GetComponents.
        /// </summary>
        private List<IPowerComponent> _components;

        /// <summary>
        /// Sosednie PowerNode (pryamye fizicheskie svyazi).
        /// Ispolzuetsya dlya BFS pri proverke svyaznosti.
        /// </summary>
        private List<PowerNode> _neighbors;

        /// <summary>Bazovoe potreblenie iz BuildableData.</summary>
        private float _basePowerRating;

        /// <summary>Prioritet iz BuildableData.</summary>
        private int _basePowerPriority;

        /// <summary>Tekuschee sostoyanie pitaniya.</summary>
        private bool _hasPower = true;
        private int _topologyRevision;
        private int _graphScratchIndex = -1;
        private int _graphScratchVersion;
        private bool _isRuptured;
        private bool _isShortCircuited;

        /// <summary>
        /// Staticheskiy bufer dlya OverlapSphereNonAlloc.
        /// 32 kollaydera — dostatochno dlya lyubogo modulya.
        /// Shared: tolko odin PowerNode spavnitsya za kadr.
        /// </summary>
        private static readonly Collider[] OverlapBuffer = new Collider[32];

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Set etogo uzla. null esli ne podklyuchen.</summary>
        public PowerGrid Grid => _grid;

        /// <summary>
        /// Vse IPowerComponent na etom module.
        /// Ispolzuetsya PowerGrid.UpdateBalance() dlya podscheta.
        /// Read-only rekomenduetsya.
        /// </summary>
        public List<IPowerComponent> Components => _components;

        /// <summary>
        /// Pryamye sosedi (podklyuchennye fizicheski).
        /// Ispolzuetsya PowerGridManager.CheckAndSplitGrid() dlya BFS.
        /// </summary>
        public List<PowerNode> Neighbors => _neighbors;
        internal int TopologyRevision => _topologyRevision;
        internal int GraphScratchIndex
        {
            get => _graphScratchIndex;
            set => _graphScratchIndex = value;
        }

        internal int GraphScratchVersion
        {
            get => _graphScratchVersion;
            set => _graphScratchVersion = value;
        }

        internal bool IsRuptured => _isRuptured;
        internal bool IsShortCircuited => _isShortCircuited;

        /// <summary>
        /// Ustanavlivaet ssylku na set.
        /// Vyzyvaetsya PowerGrid pri Add/Remove/Merge.
        /// </summary>
        public void SetGrid(PowerGrid grid)
        {
            _grid = grid;
        }

        internal void SetRuptured(bool ruptured)
        {
            if (_isRuptured == ruptured)
                return;

            _isRuptured = ruptured;
            _topologyRevision++;
        }

        internal void SetShortCircuited(bool shortCircuited)
        {
            if (_isShortCircuited == shortCircuited)
                return;

            _isShortCircuited = shortCircuited;
            _topologyRevision++;
        }

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent — BAZOVOE POTREBLENIE MODULYa
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Bazovoe energopotreblenie modulya (iz BuildableData).
        ///
        /// Eto potreblenie samogo modulya (korpus, osveschenie, ventilyatsiya).
        /// Dopolnitelnye potrebiteli (Fabricator i t.d.) imeyut
        /// svoi IPowerComponent s otdelnym PowerRating.
        ///
        /// Primery:
        ///   • Koridor: 0 (passivnyy, tolko provodit energiyu)
        ///   • Zhilaya komnata: -30 (bazovoe osveschenie)
        ///   • Solnechnaya panel: +200 (generatsiya)
        ///   • Reaktor: +500 (generatsiya)
        /// </summary>
        public float PowerRating => _basePowerRating;

        /// <summary>Prioritet otklyucheniya bazovogo potrebleniya.</summary>
        public int PowerPriority => _basePowerPriority;

        /// <summary>Tekuschee sostoyanie pitaniya (keshirovannoe).</summary>
        public bool HasPower => _hasPower;

        /// <summary>
        /// Uvedomlenie ob izmenenii pitaniya.
        /// Dlya bazovogo potrebleniya (PowerNode) — prosto keshiruem.
        /// Komponenty (Fabricator i t.d.) poluchayut svoi uvedomleniya
        /// cherez svoy IPowerComponent.OnPowerStatusChanged.
        /// </summary>
        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;

            // Buduschee: otklyuchenie/vklyuchenie bazovogo osvescheniya,
            // ventilyatsii, zvukov modulya
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _components = new List<IPowerComponent>(4);
            _neighbors  = new List<PowerNode>(6);
        }

        // ══════════════════════════════════════════════════════════
        //  IPoolable — ZhIZNENNYY TsIKL PULA
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vyzyvaetsya ObjectPoolManager posle SetActive(true).
        ///
        /// Poryadok:
        ///   1. Chitaem BuildableData cherez ModuleMarker.
        ///   2. Sobiraem vse IPowerComponent na obekte.
        ///   3. Ischem sosednie PowerNode.
        ///   4. Podklyuchaemsya k seti (ili sozdaem novuyu).
        /// </summary>
        public void OnSpawn()
        {
            _hasPower = true;

            // ── 1. Chitaem dannye iz BuildableData ──
            ReadBuildableData();

            // ── 2. Sobiraem IPowerComponent ──
            if (_components == null)
                _components = new List<IPowerComponent>(4);
            else
                _components.Clear();

            GetComponents(_components); // zero GC, fills list

            // ── 3. Ischem sosedey i podklyuchaemsya k seti ──
            if (_neighbors == null)
                _neighbors = new List<PowerNode>(6);
            else
                _neighbors.Clear();

            FindAndConnectNeighbors();
        }

        /// <summary>
        /// Vyzyvaetsya ObjectPoolManager pered SetActive(false).
        ///
        /// Poryadok:
        ///   1. Otklyuchaemsya ot seti.
        ///   2. Ubiraem sebya iz spiskov sosedey.
        ///   3. Proveryaem svyaznost ostavsheysya seti.
        ///   4. Ochischaem keshi.
        /// </summary>
        public void OnDespawn()
        {
            DisconnectFromGrid();
            RemoveSelfFromNeighbors();

            _neighbors.Clear();
            _components.Clear();
            _grid = null;
            _hasPower = true;
            _graphScratchIndex = -1;
            _graphScratchVersion = 0;
            _isRuptured = false;
            _isShortCircuited = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DATA-DRIVEN INITIALIZATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Chitaet powerRating i powerPriority iz BuildableData
        /// cherez ModuleMarker na etom obekte.
        ///
        /// Esli ModuleMarker otsutstvuet — ispolzuyutsya fallback znacheniya
        /// iz Inspector.
        ///
        /// Primechanie: Trebuet chto ModuleMarker imeet publichnoe svoystvo
        /// Data tipa BuildableData. Esli ego net — dobavte:
        ///   public BuildableData Data => _buildableData;
        /// </summary>
        private void ReadBuildableData()
        {
            _basePowerRating   = fallbackPowerRating;
            _basePowerPriority = fallbackPowerPriority;

            if (TryGetComponent(out ModuleMarker marker))
            {
                BuildableData data = marker.Data;
                if (data != null)
                {
                    _basePowerRating   = data.powerRating;
                    _basePowerPriority = data.powerPriority;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — NEIGHBOR DISCOVERY & GRID CONNECTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ischet sosednie PowerNode cherez OverlapSphereNonAlloc.
        /// Podklyuchaetsya k suschestvuyuschey seti ili sozdaet novuyu.
        /// Pri obnaruzhenii sosedey iz raznyh setey — obedinyaet.
        ///
        /// ZERO GC: static Collider[] buffer, TryGetComponent,
        /// ReferenceEquals dlya proverki dublikatov.
        ///
        /// STsENARIY OBEDINENIYa:
        ///   Igrok stavit koridor mezhdu dvumya nezavisimymi komnatami.
        ///   Koridor nahodit sosedey iz GridA i GridB.
        ///   → MergeGrids(GridA, GridB) → odna obschaya set.
        /// </summary>
        private void FindAndConnectNeighbors()
        {
            bool topologyChanged = false;
            int overlapCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                transform.position,
                connectionRadius,
                OverlapBuffer,
                connectionMask,
                QueryTriggerInteraction.Ignore);

            PowerGrid targetGrid = null;

            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col == null) continue;
                if (ReferenceEquals(col.gameObject, gameObject)) continue;

                if (!col.TryGetComponent(out PowerNode neighbor)) continue;
                if (ReferenceEquals(neighbor, this)) continue;

                // ── Registriruem kak soseda (dvustoronnyaya svyaz) ──
                if (!ContainsRef(_neighbors, neighbor))
                {
                    _neighbors.Add(neighbor);
                    topologyChanged = true;
                }

                if (!ContainsRef(neighbor._neighbors, this))
                {
                    neighbor._neighbors.Add(this);
                    neighbor._topologyRevision++;
                }

                // ── Setevaya logika ──
                if (neighbor._grid != null)
                {
                    if (targetGrid == null)
                    {
                        // Pervyy naydennyy sosed s setyu → prisoedinyaemsya
                        targetGrid = neighbor._grid;
                    }
                    else if (!ReferenceEquals(targetGrid, neighbor._grid))
                    {
                        // Sosed iz DRUGOY seti → obedinyaem!
                        targetGrid = PowerGridManager.MergeGrids(
                            targetGrid, neighbor._grid);
                    }
                }
            }

            // ── Podklyuchenie k seti ──
            if (targetGrid != null)
            {
                // Prisoedinyaemsya k naydennoy seti
                targetGrid.AddNode(this);
                _grid = targetGrid;
            }
            else
            {
                // Net sosedey s setyu → sozdaem svoyu
                _grid = PowerGridManager.CreateGrid(this);
            }

            if (topologyChanged)
                _topologyRevision++;
        }

        /// <summary>
        /// Otklyuchaetsya ot tekuschey seti.
        /// Proveryaet svyaznost ostavshihsya uzlov.
        /// Esli set raspalas — razdelyaet.
        /// </summary>
        private void DisconnectFromGrid()
        {
            if (_grid == null) return;

            PowerGrid oldGrid = _grid;
            oldGrid.RemoveNode(this);

            // ── Proverka svyaznosti ──
            if (oldGrid.NodeCount > 1)
            {
                // Set mogla raspastsya — proveryaem BFS
                PowerGridManager.CheckAndSplitGrid(oldGrid);
            }
            else if (oldGrid.NodeCount == 0)
            {
                // Set pusta — udalyaem
                PowerGridManager.DestroyGrid(oldGrid);
            }
            // Esli NodeCount == 1 — set iz odnogo uzla, svyazna po opredeleniyu

            _grid = null;
        }

        /// <summary>
        /// Ubiraet sebya iz spiskov sosedey vseh podklyuchennyh uzlov.
        /// Vyzyvaetsya pri despavne.
        /// </summary>
        private void RemoveSelfFromNeighbors()
        {
            int count = _neighbors.Count;
            bool topologyChanged = count > 0;
            for (int i = 0; i < count; i++)
            {
                PowerNode neighbor = _neighbors[i];
                if (neighbor == null) continue;

                if (RemoveRef(neighbor._neighbors, this))
                    neighbor._topologyRevision++;
            }

            if (topologyChanged)
                _topologyRevision++;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — COLLECTION HELPERS (Zero GC)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Proverka nalichiya po ssylke. Zero GC.
        /// O(n) — no spiski sosedey obychno 1-6 elementov.
        /// </summary>
        private static bool ContainsRef<T>(List<T> list, T item) where T : class
        {
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(list[i], item))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Udalenie po ssylke. Obychnyy RemoveAt (ne swap — sohranyaem poryadok).
        /// </summary>
        private static bool RemoveRef<T>(List<T> list, T item) where T : class
        {
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(list[i], item))
                {
                    list.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            if (connectionRadius < 0.5f) connectionRadius = 0.5f;
        }

        private void OnDrawGizmosSelected()
        {
            // ── Radius podklyucheniya ──
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.12f);
            Gizmos.DrawWireSphere(transform.position, connectionRadius);

            if (!Application.isPlaying) return;

            // ── Svyazi s sosedyami ──
            if (_neighbors != null)
            {
                int count = _neighbors.Count;
                for (int i = 0; i < count; i++)
                {
                    PowerNode neighbor = _neighbors[i];
                    if (neighbor == null) continue;

                    // Tsvet zavisit ot sostoyaniya pitaniya
                    Gizmos.color = (_hasPower && neighbor._hasPower)
                        ? Color.green
                        : Color.red;

                    Gizmos.DrawLine(transform.position, neighbor.transform.position);
                }
            }

            // ── Informatsiya o seti ──
            if (_grid != null)
            {
                string info = $"Grid #{_grid.Id}\n" +
                              $"Nodes: {_grid.NodeCount}\n" +
                              $"Gen: {_grid.TotalGeneration:F0}W\n" +
                              $"Con: {_grid.TotalConsumption:F0}W\n" +
                              $"Bal: {_grid.Balance:F0}W";

                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2f,
                    info,
                    new GUIStyle
                    {
                        fontSize = 10,
                        normal =
                        {
                            textColor = _grid.HasPowerDeficit
                                ? Color.red
                                : Color.green
                        }
                    });
            }
        }
#endif
    }
}
