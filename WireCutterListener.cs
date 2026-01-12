using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;
using System;
using System.Collections.Generic;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class WireCutterListener : MonoBehaviour
    {
        public WireCutterListener(IntPtr ptr) : base(ptr) { }
        public WireCutterListener() : base(ClassInjector.DerivedConstructorPointer<WireCutterListener>())
            => ClassInjector.DerivedConstructorBody(this);

        public PickUp wireCutter;
        public WireCutterHitBox hitBox;
        SparkDriver sparkDriver;

        void Start()
        {
            wireCutter = GetComponent<PickUp>();
            if (wireCutter == null)
            {
                MelonLogger.Warning("[WireCutterListener] - PickUp component not found on WireCutters.");
                return;
            }

            wireCutter._OnUseEvent.AddListener((UnityEngine.Events.UnityAction)OnUsed);


            GameObject hitBoxObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hitBoxObj.transform.parent = gameObject.transform;
            if (hitBoxObj.GetComponent<MeshRenderer>() == null) hitBoxObj.AddComponent<MeshRenderer>();
            hitBoxObj.GetComponent<MeshRenderer>().enabled = false;
            hitBoxObj.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
            hitBoxObj.transform.localPosition = new Vector3(0.1f, -0.06f, 0f);

            SphereCollider col = hitBoxObj.GetComponent<SphereCollider>();
            if (col == null) col = hitBoxObj.AddComponent<SphereCollider>();
            col.isTrigger = true;

            hitBox = hitBoxObj.AddComponent<WireCutterHitBox>();
            sparkDriver = hitBoxObj.AddComponent<SparkDriver>();
        }

        public void playSpark()
        {
            MelonLogger.Msg("[Sparky] - WireCutterListener played spark");
            sparkDriver.TriggerBurst();
        }

        void OnUsed()
        {
            MelonLogger.Msg("WIRECUTTERS USED");

            if (hitBox == null)
            {
                MelonLogger.Warning("[WireCutterListener] - hitBox is null in OnUsed().");
                return;
            }

            GameObject cutNode = hitBox.getClosestNodeToPoint();
            if (cutNode == null)
            {
                MelonLogger.Warning("[WireCutterListener] - No node in hitbox when used.");
                return;
            }

            Transform parent = cutNode.transform.parent;
            if (parent == null)
            {
                MelonLogger.Warning("[WireCutterListener] - cutNode has no parent: " + cutNode.name);
                return;
            }

            Wire wireToCut = parent.GetComponent<Wire>();
            if (wireToCut == null)
            {
                MelonLogger.Warning("[WireCutterListener] - No Wire component on parent of node: " + cutNode.name + " (parent=" + parent.name + ")");
                return;
            }

            int cutIndex = getNodeIndex(wireToCut, cutNode);
            if (cutIndex == -1)
            {
                MelonLogger.Warning("[WireCutterListener] - Could not find node index in OnUsed() for node: " + cutNode.name);
                return;
            }

            MelonLogger.Msg("[WireCutterListener] - Cutting wire '" + wireToCut.gameObject.name + "' at index " + cutIndex);
            wireToCut.CutWire(cutIndex);
        }

        private int getNodeIndex(Wire wire, GameObject node)
        {
            if (wire == null)
            {
                MelonLogger.Warning("[WireCutterListener] getNodeIndex: wire is null");
                return -1;
            }

            if (node == null)
            {
                MelonLogger.Warning("[WireCutterListener] getNodeIndex: node is null");
                return -1;
            }

            if (wire.nodes == null)
            {
                MelonLogger.Warning("[WireCutterListener] getNodeIndex: wire.nodes is null on " + wire.gameObject.name);
                return -1;
            }

            for (int i = 0; i < wire.nodes.Length; i++)
            {
                if (wire.nodes[i] == node)
                    return i;
            }

            MelonLogger.Warning("[WireCutterListener] getNodeIndex: node '" + node.name + "' not found in wire '" + wire.gameObject.name + "'. nodes.Length=" + wire.nodes.Length);
            return -1;
        }
    }


    public class WireCutterHitBox : MonoBehaviour
    {
        public WireCutterHitBox(IntPtr ptr) : base(ptr) { }
        public WireCutterHitBox() : base(ClassInjector.DerivedConstructorPointer<WireCutterHitBox>())
            => ClassInjector.DerivedConstructorBody(this);

        private readonly List<GameObject> nodesInHB = new List<GameObject>();

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            if (other.name.ToLower().Contains("node"))
            {
                if (!nodesInHB.Contains(other.gameObject))
                {
                    nodesInHB.Add(other.gameObject);
                    MelonLogger.Msg("[WireCutterHitBox] - Node entered: " + other.gameObject.name + " (count=" + nodesInHB.Count + ")");
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null) return;

            if (nodesInHB.Contains(other.gameObject))
            {
                nodesInHB.Remove(other.gameObject);
                MelonLogger.Msg("[WireCutterHitBox] - Node exited: " + other.gameObject.name + " (count=" + nodesInHB.Count + ")");
            }
        }

        public GameObject getClosestNodeToPoint()
        {
            if (nodesInHB == null || nodesInHB.Count == 0)
            {
                MelonLogger.Warning("[WireCutterHitBox] getClosestNodeToPoint: nodesInHB is empty.");
                return null;
            }

            GameObject closestNode = null;
            float closestDist = float.MaxValue;
            Vector3 pos = transform.position;

            for (int i = 0; i < nodesInHB.Count; i++)
            {
                GameObject node = nodesInHB[i];
                if (node == null) continue;

                float dist = Vector3.Distance(pos, node.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestNode = node;
                }
            }

            if (closestNode == null)
            {
                MelonLogger.Warning("[WireCutterHitBox] getClosestNodeToPoint: all nodes were null.");
                return null;
            }

            MelonLogger.Msg("[WireCutterHitBox] Closest node is " + closestNode.name + " at distance " + closestDist);
            return closestNode;
        }
    }
}
