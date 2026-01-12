
using IEYTD2_SubmarineCode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnhollowerRuntimeLib;
using UnityEngine;
using System;
using MelonLoader;
using UnityEngine.Rendering;

namespace IEYTD2_SubmarineCode
{
    public class WireManager : MonoBehaviour
    {
        public WireManager(IntPtr ptr) : base(ptr) { }
        public WireManager() : base(ClassInjector.DerivedConstructorPointer<WireManager>())
            => ClassInjector.DerivedConstructorBody(this);
        //public Transform[] wireStaticPoints = new Transform[4];
        //[0] = Socket1, [1] = Socket2, [2] = Control1, [3] = Control2

        public GameObject ReactorHub;
        public GameObject TerminalHub;
        public GameObject CoolantHub;

        public List<Wire> wires = new List<Wire>();
        public int wireCount = 0;
        public Material[] wireMaterials;

        public bool rightHandCarrying = false;
        public bool leftHandCarrying = false;

        public bool _debugCutWire = false;
        public float scale = 0.01f;

        public int _checkPoint = 0;

        public WireClamp clamp1;
        public WireClamp clamp2;
        public WireClamp clamp3;

        private void printCheckpoint()
        {
            MelonLogger.Msg("[WireManager] - Checkpoint " + (_checkPoint++));
        }

        public void Start()
        {
            printCheckpoint();
            ReactorHub = GameObject.Find("ReactorElectricHub");
            TerminalHub = GameObject.Find("TerminalElectricHub");
            CoolantHub = GameObject.Find("CoolantElectricHub");
            wireMaterials = GameObject.Find("WireMaterials").GetComponent<MeshRenderer>().materials;
            printCheckpoint();

            if (ReactorHub == null || TerminalHub == null || CoolantHub == null)
            {
                MelonLogger.Warning("[WireManager] - One of the hubs is null, aborting Start");
                return; //bail out so we don't crash using null hubs
            }
            printCheckpoint();

            Transform[] rs = new Transform[6]; // reactor sockets
            Transform[] ts = new Transform[6]; // terminal sockets
            Transform[] cs = new Transform[6]; // coolant sockets
            printCheckpoint();

            //Reactor sockets
            int childCount = ReactorHub.transform.childCount;
            int limit = childCount < rs.Length ? childCount : rs.Length;
            for (int i = 0; i < limit; i++)
            {
                rs[i] = ReactorHub.transform.GetChild(i);
            }
            printCheckpoint();

            //Terminal sockets
            childCount = TerminalHub.transform.childCount;
            limit = childCount < ts.Length ? childCount : ts.Length;
            for (int i = 0; i < limit; i++)
            {
                ts[i] = TerminalHub.transform.GetChild(i);
            }

            //Coolant sockets
            childCount = CoolantHub.transform.childCount;
            limit = childCount < cs.Length ? childCount : cs.Length;
            for (int i = 0; i < limit; i++)
            {
                cs[i] = CoolantHub.transform.GetChild(i);
            }

            printCheckpoint();


            wires.Add(createWire(formatCP(rs[0], cs[3]),0));
            wires[0].Color = "Red";
            wires.Add(createWire(formatCP(rs[1], cs[4]),1));
            wires[1].Color = "Green";
            wires.Add(createWire(formatCP(rs[2], cs[5]),3));
            wires[2].Color = "Yellow";
            wires.Add(createWire(formatCP(rs[3], ts[2]),6));
            wires[3].Color = "Burnt";
            wires[3].nodes[wires[3].nodes.Length / 2].AddComponent<SparkDriver>().EnableLoop(true);
            wires.Add(createWire(formatCP(rs[4], ts[1]),7));
            wires[4].Color = "Burnt";
            wires[4].nodes[wires[4].nodes.Length / 2].AddComponent<SparkDriver>().EnableLoop(true);
            wires.Add(createWire(formatCP(rs[5], ts[0]),9));
            wires[5].Color = "Burnt";
            wires[5].nodes[wires[5].nodes.Length / 2].AddComponent<SparkDriver>().EnableLoop(true);
            wires.Add(createWire(formatCP(ts[3], cs[2]),2));
            wires[6].Color = "Blue";
            wires.Add(createWire(formatCP(ts[4], cs[1]),4));
            wires[7].Color = "Orange";
            wires.Add(createWire(formatCP(ts[5], cs[0]),5));
            wires[8].Color = "Purple";

            printCheckpoint();

            /*
             * Set up Clamps
             */
            clamp1 = GameObject.Find("WireClip1").AddComponent<WireClamp>();
            clamp2 = GameObject.Find("WireClip2").AddComponent<WireClamp>();
            clamp3 = GameObject.Find("WireClip3").AddComponent<WireClamp>();


            var leftHand = GameObject.Find("LeftHand");
            var rightHand = GameObject.Find("RightHand");
            if (GameObject.Find("RightHandGB") == null)
            {
                GameObject rightHandGB = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rightHandGB.name = "RightHandGB";
                rightHandGB.transform.parent = rightHand.transform;
                rightHandGB.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
                if (rightHandGB.GetComponent<SphereCollider>() == null) rightHandGB.AddComponent<SphereCollider>();
                rightHandGB.GetComponent<SphereCollider>().isTrigger = true;
                if (rightHandGB.GetComponent<Rigidbody>() == null) rightHandGB.AddComponent<Rigidbody>();
                rightHandGB.GetComponent<Rigidbody>().isKinematic = true;
                rightHandGB.GetComponent<MeshRenderer>().enabled = false;
                rightHandGB.transform.localPosition = new Vector3(-0.03f, 0, 0.1f);

                GameObject leftHandGB = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leftHandGB.name = "LeftHandGB";
                leftHandGB.transform.parent = leftHand.transform;
                leftHandGB.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
                if (leftHandGB.GetComponent<SphereCollider>() == null) leftHandGB.AddComponent<SphereCollider>();
                leftHandGB.GetComponent<SphereCollider>().isTrigger = true;
                if (leftHandGB.GetComponent<Rigidbody>() == null) leftHandGB.AddComponent<Rigidbody>();
                leftHandGB.GetComponent<Rigidbody>().isKinematic = true;
                leftHandGB.GetComponent<MeshRenderer>().enabled = false;
                leftHandGB.transform.localPosition = new Vector3(-0.03f, 0, 0.1f);

            }



            printCheckpoint(); // should say 7
        }

        public void Update()
        {
            if (_debugCutWire)
            {
                _debugCutWire = false;
                CutWire(wires[5], 8);
            }
        }

        private Wire createWire(Transform[] controlPoints, int matIndex)
        {
            int nodeCount = 15; //How many nodes in each wire

            Vector3[] positions = BezierCurveCreator.SampleCubic(
                controlPoints[0],
                controlPoints[2],
                controlPoints[3],
                controlPoints[1],
                nodeCount,
                approxEqualDistance: true
            );

            GameObject[] nodes = createNodes(positions, new Transform[] { controlPoints[0], controlPoints[1] });

            GameObject wireVisual = new GameObject("WireVisual");
            WireTubeRenderer wr = wireVisual.AddComponent<WireTubeRenderer>();
            wireVisual.GetComponent<MeshRenderer>().material = wireMaterials[matIndex];
            wr.nodes = toTransform(nodes);
            wr.radius = 0.55f * scale;

            GameObject parent = new GameObject("Wire" + (wireCount++));
            parent.transform.position = controlPoints[0].position;

            foreach (GameObject node in nodes)
            {
                node.transform.parent = parent.transform;
            }

            wireVisual.transform.parent = parent.transform;

            Wire wire = parent.AddComponent<Wire>();
            wire.Socket1 = controlPoints[0];
            wire.Socket2 = controlPoints[1];
            wire.Control1 = controlPoints[2];
            wire.Control2 = controlPoints[3];
            wire.parent = parent;
            wire.WireVisual = wireVisual;
            wire.nodes = nodes;
            wire.CutState = false;
            wire.Material = wireMaterials[matIndex];
            wire.wm = this;
            wire.sll = gameObject.GetComponent<SubmarineLevelLogic>();

            return wire;
        }

        private GameObject[] createNodes(Vector3[] positions, Transform[] sockets)
        {
            List<GameObject> nodes = new List<GameObject>();

            GameObject topAnchor = new GameObject("TopAnchor");
            topAnchor.transform.position = sockets[0].position;
            topAnchor.AddComponent<Rigidbody>().isKinematic = true;

            GameObject bottomAnchor = new GameObject("BottomAnchor");
            bottomAnchor.transform.position = sockets[1].position;
            bottomAnchor.AddComponent<Rigidbody>().isKinematic = true;

            nodes.Add(topAnchor);

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                node.name = "Node" + i;
                node.transform.localScale = new Vector3(0.1f * scale, 0.1f * scale, 0.1f * scale);
                node.transform.position = positions[i];
                node.AddComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                node.GetComponent<MeshRenderer>().enabled = false;
                nodes.Add(node);
            }

            nodes.Add(bottomAnchor);
            return nodes.ToArray();
        }

        private Transform[] toTransform(GameObject[] nodes)
        {
            Transform[] transforms = new Transform[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                transforms[i] = nodes[i].transform;
            }
            return transforms;
        }

        private Transform[] formatCP(Transform socket1, Transform socket2)
        {
            Transform[] formatted = new Transform[4];
            formatted[0] = socket1;
            formatted[1] = socket2;
            formatted[2] = socket1.GetChild(0);
            formatted[3] = socket2.GetChild(0);
            return formatted;
        }

        public bool coolantCut = false;
        public void CutWire(Wire wire, int index)
        {
            AudioUtil.PlayAt("wireCut", wire.transform.position);
            wire.CutWire(index);
            string color = wire.Color;
            if((color == "Red" || color == "Green" || color == "Yellow") && !coolantCut)
            {
                coolantCut = true;
                MelonLogger.Msg("RGY wire cut");
                ObjectBank.Instance.Manager.GetComponent<SubmarineLevelLogic>().toggleChip("Blue Chip", false);
                LoopingSfx coolantAudio = ObjectBank.Instance.Manager.GetComponent<SubmarineLevelLogic>().coolantAudio;
                AudioUtil.PlayAt("propeller_deactivation", coolantAudio.transform.position);
                coolantAudio.TurnOff();
                
            }
        }

        public void clampSoldered()
        {
            if(clamp1.Connection && clamp2.Connection && clamp3.Connection)
            {
                ObjectBank.Instance.Manager.GetComponent<SubmarineLevelLogic>().toggleChip("Green Chip", true);
                gameObject.GetComponent<SubmarineLevelLogic>().endBlackout();
            }
        }
    }

    public class Wire : MonoBehaviour
    {
        public Transform Socket1, Socket2, Control1, Control2;
        public GameObject[] nodes;
        public GameObject parent;
        public GameObject WireVisual;
        public bool CutState;
        public GameObject _Handle1;
        public GameObject _Handle2;
        public Material Material;
        public string Color;
        public SubmarineLevelLogic sll;

        public WireManager wm;

        public Wire(IntPtr ptr) : base(ptr) { }
        public Wire() : base(ClassInjector.DerivedConstructorPointer<Wire>())
            => ClassInjector.DerivedConstructorBody(this);


       // bool coolantCut = false;
        public void CutWire(int index)
        {
            AudioUtil.PlayAt("wireCut", transform.position);
            if (Color == "Burnt")
            {
                //Die
                AudioUtil.PlayAt("spark_05", transform.position);
                MelonLogger.Msg("[CutWire] - Cut burnt wire");
                ObjectBank.Instance.ELV_WireCutters_Gathered.GetComponent<WireCutterListener>().playSpark();
                sll.KillPlayer();
                return;
            }

            if ((Color == "Red" || Color == "Green" || Color == "Yellow") && !wm.coolantCut)
            {
                wm.coolantCut = true;
                MelonLogger.Msg("RGY wire cut");
                ObjectBank.Instance.Manager.GetComponent<SubmarineLevelLogic>().toggleChip("Blue Chip", false);
                LoopingSfx coolantAudio = ObjectBank.Instance.Manager.GetComponent<SubmarineLevelLogic>().coolantAudio;
                AudioUtil.PlayAt("propeller_deactivation", coolantAudio.transform.position);
                coolantAudio.TurnOff();

            }

            Destroy(WireVisual);

            if (index >= nodes.Length) return;

            GameObject firstHalf = new GameObject("First Half");
            GameObject secondHalf = new GameObject("Second Half");

            firstHalf.transform.parent = parent.transform;
            firstHalf.transform.localPosition = Vector3.zero;
            firstHalf.transform.localRotation = Quaternion.identity;
            firstHalf.transform.localScale = Vector3.one;

            secondHalf.transform.parent = parent.transform;
            secondHalf.transform.localPosition = Vector3.zero;
            secondHalf.transform.localRotation = Quaternion.identity;
            secondHalf.transform.localScale = Vector3.one;

            GameObject[] nodes1 = new GameObject[index];
            GameObject[] nodes2 = new GameObject[nodes.Length - index];
            for (int i = 0; i < index; i++)
            {
                nodes1[i] = nodes[i]; //Top half --> nodes1 starting at topAnchor
                nodes1[i].transform.parent = firstHalf.transform;
            }
            int _n2count = 0; //cause nodes2 index and nodes index are different for this one
            for (int i = (nodes.Length - 1); i >= index; i--)
            {
                nodes2[_n2count] = nodes[i];
                nodes2[_n2count].transform.parent = secondHalf.transform;
                _n2count++;
            }
            nodes1[nodes1.Length - 1].name = "Handle";
            nodes2[nodes2.Length - 1].name = "Handle";

            /*
             * ADD LOOSEWIRE COMPONENTS
             */
            LooseWire lw1 = firstHalf.AddComponent<LooseWire>();
            lw1.nodes = toTransform(nodes1);
            lw1.wm = wm;

            LooseWire lw2 = secondHalf.AddComponent<LooseWire>();
            lw2.nodes = toTransform(nodes2);
            lw2.wm = wm;


            /*
             * ADD VISUALS
             */
            GameObject wv1 = new GameObject("Wire Visual");
            wv1.transform.parent = firstHalf.transform;
            WireTubeRenderer firstHalfVisual = wv1.AddComponent<WireTubeRenderer>();
            firstHalfVisual.nodes = toTransform(nodes1);
            firstHalfVisual.gameObject.GetComponent<MeshRenderer>().material = Material;
            firstHalfVisual.radius = 0.55f * wm.scale;

            lw1.visual = firstHalfVisual;
            lw1.Color = Color;

            GameObject wv2 = new GameObject("Wire Visual");
            wv2.transform.parent = secondHalf.transform;
            WireTubeRenderer secondHalfVisual = wv2.AddComponent<WireTubeRenderer>();
            secondHalfVisual.nodes = toTransform(nodes2);
            secondHalfVisual.gameObject.GetComponent<MeshRenderer>().material = Material;
            secondHalfVisual.radius = 0.55f * wm.scale;
            lw2.visual = secondHalfVisual;
            lw2.Color = Color;

            


            CutState = true;
        }

        private Transform[] toTransform(GameObject[] nodes)
        {
            Transform[] transforms = new Transform[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                transforms[i] = nodes[i].transform;
            }
            return transforms;
        }

        private void printNodes(GameObject[] nodes, string nodeName)
        {
            Debug.Log(nodeName + ":");
            int count = 0;
            foreach (GameObject node in nodes)
            {
                Debug.Log(count++ + ": " + node);
            }
        }
    }

    public class WireClamp : MonoBehaviour
    {
        public Transform[] clampNodes = new Transform[7];
        public bool[] clampNodesOccupied = new bool[2];
        public LooseWire slot1;
        public LooseWire slot2;
        public GameObject solderObject;
        public bool Connection = false;
        ObjectBank bank;

        public WireClamp(IntPtr ptr) : base(ptr) { }
        public WireClamp() : base(ClassInjector.DerivedConstructorPointer<WireClamp>())
            => ClassInjector.DerivedConstructorBody(this);


        public void Start()
        {
            Transform parent = this.transform;
            int childCount = parent.childCount;
            bank = ObjectBank.Instance;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = parent.GetChild(i);
                switch (child.name)
                {
                    case "MiddlePoint":
                        clampNodes[3] = child;
                        break;
                    case "Bottom1":
                        clampNodes[0] = child;
                        break;
                    case "Bottom2":
                        clampNodes[1] = child;
                        break;
                    case "Bottom3":
                        clampNodes[2] = child;
                        break;
                    case "Top1":
                        clampNodes[4] = child;
                        break;
                    case "Top2":
                        clampNodes[5] = child;
                        break;
                    case "Top3":
                        clampNodes[6] = child;
                        break;
                    default:
                        Debug.Log("[WireClamp] Clamp child name not matched: " + child.name);
                        break;
                }
            }
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            FitBoxToRenderer();
            box.isTrigger = true;
            solderObject = Instantiate(ObjectBank.Instance.SolderObj);
           // solderObject.transform.parent = transform;
            

        }

        void FitBoxToRenderer()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();

            var rend = GetComponentInChildren<Renderer>();
            if (rend == null) return;

            Bounds b = rend.bounds;

            // world -> local
            box.center = transform.InverseTransformPoint(b.center);

            Vector3 localSize = transform.InverseTransformVector(b.size);
            box.size = new Vector3(
                Mathf.Abs(localSize.x),
                Mathf.Abs(localSize.y),
                Mathf.Abs(localSize.z)
            );
        }


        public void OnTriggerStay(Collider other)
        {
            // Debug.Log("pluh: " + other.name) ;
            if (other.name == "Handle")
            {
                LooseWire wire = other.gameObject.transform.parent.GetComponent<LooseWire>();
                if (wire == null)
                {
                    Debug.Log("ontriggerstay wire null");
                    return;
                }
                if ((!wire.follow) && !wire.isClamped)
                {
                    HookWire(wire);
                    Debug.Log("Hook em");
                }
            }
        }

        


        public void Solder()
        {
            if (Connection) return;
            if (slot1 == null || slot2 == null) return;

            if (!ColorsMatch())
            {
                //Die     
                MelonLogger.Msg("[Solder] - colors dont match");
                AudioUtil.PlayAt("spark_05", transform.position);
                bank.SolderingGun.GetComponent<SolderListener>().playSpark();
                bank.Manager.GetComponent<SubmarineLevelLogic>().KillPlayer();
                return;
            }
            Transform end1 = slot1.nodes[slot1.nodes.Length - 1];
            Transform end2 = slot2.nodes[slot2.nodes.Length - 1];
            PositionSolderBetween(end1.position, end2.position);
            foreach (LooseWire wire in new LooseWire[] { slot1, slot2 })
            {
                wire.visual.enabled = false;
                foreach (Transform node in wire.nodes)
                {
                    Destroy(node.gameObject);
                }
            }

            Connection = true;
            //solderObject.transform.position = clampNodes[3].position;
            AudioUtil.PlayAt("spark", transform.position);
            gameObject.GetComponent<BoxCollider>().enabled = false;
            ObjectBank.Instance.Manager.GetComponent<WireManager>().clampSoldered();
        }



        private bool ColorsMatch()
        {
            //To Do
            switch(slot1.Color)
            {
                case "Red": return slot2.Color == "Blue";
                case "Blue": return slot2.Color == "Red";
                case "Orange": return slot2.Color == "Green";
                case "Green": return slot2.Color == "Orange";
                case "Yellow": return slot2.Color == "Purple";
                case "Purple": return slot2.Color == "Yellow";
                default: return false;
            }
        }

        public void HookWire(LooseWire wire)
        {
            wire.isClamped = true;
            GameObject wireEndNode = wire.nodes[wire.nodes.Length - 1].gameObject;

            Transform[] clampPoints;

            if (slot1 != null && slot2 != null)
                return;
            else if (slot1 == null && slot2 == null)
            {
                if (Vector3.Distance(wireEndNode.transform.position, clampNodes[0].position) <
                    Vector3.Distance(wireEndNode.transform.position, clampNodes[6].position))
                {
                    clampPoints = new Transform[] { clampNodes[0], clampNodes[1], clampNodes[2] };
                    slot1 = wire;
                }
                else
                {
                    clampPoints = new Transform[] { clampNodes[4], clampNodes[5], clampNodes[6] };
                    slot2 = wire;
                }
            }
            else
            {
                if (slot1 != null)
                {
                    clampPoints = new Transform[] { clampNodes[4], clampNodes[5], clampNodes[6] };
                    slot2 = wire;
                }
                else
                {
                    clampPoints = new Transform[] { clampNodes[0], clampNodes[1], clampNodes[2] };
                    slot1 = wire;
                }
            }

            wire.clampWire(clampPoints);
            wire.clamp = this;
        }

        private void PositionSolderBetween(Vector3 p1, Vector3 p2)
        {
            if (solderObject == null)
                return;

            //Midpoint between ends
            Vector3 mid = (p1 + p2) * 0.5f;

            //Direction from wire 1 tip to wire 2 tip
            Vector3 dir = p2 - p1;
            float len = dir.magnitude;
            if (len < 1e-4f) len = 1e-4f;
            dir /= len;

            solderObject.transform.position = mid;

            solderObject.transform.rotation = Quaternion.LookRotation(dir, transform.up);

            Vector3 s = solderObject.transform.localScale;

            const float baseLength = 0.02f;
            float stretch = len / baseLength;

            solderObject.transform.localScale = new Vector3(s.x, s.y, s.z * stretch);

            solderObject.SetActive(true);
        }

    }
}
