namespace Dreamteck.Forever
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    [AddComponentMenu("Dreamteck/Forever/Builders/Mesh Batcher")]
    public class MeshBatcher : Builder
    {
        public bool includeParent = false;
        public enum Mode { Cached, Dynamic }
        public Mode mode = Mode.Cached;
        [SerializeField]
        [HideInInspector]
        Batchable[] batchables = new Batchable[0];
        List<CombineChildQueue> combineQueues = new List<CombineChildQueue>();
        Mesh[] combinedMeshes = new Mesh[0];

        internal class CombineChildQueue
        {
            private Material[] materials = new Material[0];
            private Transform parent;
            private List<Batchable> batchers = new List<Batchable>();
            private int vertexCount = 0;

            internal CombineChildQueue(Transform p, Material[] m)
            {
                parent = p;
                materials = m;
            }

            internal Mesh Combine(string name)
            {
                if (batchers.Count == 0) return null;
                GameObject combined = new GameObject(name);
                combined.transform.parent = parent;
                combined.transform.localPosition = Vector3.zero;
                combined.transform.localRotation = Quaternion.identity;
                combined.transform.localScale = Vector3.one;
                MeshFilter combinedFilter = combined.AddComponent<MeshFilter>();
                MeshRenderer combinedRenderer = combined.AddComponent<MeshRenderer>();
                int instanceCount = 0;
                for (int i = 0; i < batchers.Count; i++) instanceCount += batchers[i].GetMesh().subMeshCount;
                CombineInstance[] combineInstances = new CombineInstance[instanceCount];
                int instanceIndex = 0;
                for (int i = 0; i < batchers.Count; i++)
                {
                    for (int j = 0; j < batchers[i].GetMesh().subMeshCount; j++)
                    {
                        combineInstances[instanceIndex].mesh = batchers[i].GetMesh();
                        combineInstances[instanceIndex].subMeshIndex = j;
                        combineInstances[instanceIndex].transform = parent.worldToLocalMatrix * batchers[i].transform.localToWorldMatrix;
                        instanceIndex++;
                    }
                }
                Mesh mesh = new Mesh();
                mesh.name = name;
                mesh.CombineMeshes(combineInstances, true, true);
                combinedFilter.sharedMesh = mesh;
                combinedRenderer.sharedMaterials = materials;
                return mesh;
            }

            internal bool Add(Batchable batcher)
            {
                if (!CanAddBatcher(batcher)) return false;
                vertexCount += batcher.GetMesh().vertexCount;
                batchers.Add(batcher);
                return true;
            }

            private bool CanAddBatcher(Batchable batcher)
            {
                if (batcher.GetMesh().vertexCount + vertexCount > 65000) return false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != batcher.GetMaterials()[i]) return false;
                }
                return true;
            }
        }

#if UNITY_EDITOR
        public override void OnPack()
        {
            base.OnPack();
            if (mode == Mode.Cached)
            {
                GetBatchers();
                for (int i = 0; i < batchables.Length; i++) batchables[i].Pack();
            }
            else
            {
                List<Transform> children = new List<Transform>();
                SceneUtility.GetChildrenRecursively(transform, ref children); 
                for (int i = 0; i < children.Count; i++)
                {
                    if (i == 0 && !includeParent) continue;
                    Batchable batchable = children[i].GetComponent<Batchable>();
                    if (batchable != null) batchable.Pack();
                }
                batchables = new Batchable[0];
            }
        }

        public override void OnUnpack()
        {
            base.OnUnpack();
            if (batchables.Length > 0)
            {
                for (int i = 0; i < batchables.Length; i++) batchables[i].Unpack();
            }
            else
            {
                List<Transform> children = new List<Transform>();
                SceneUtility.GetChildrenRecursively(transform, ref children);
                for (int i = 0; i < children.Count; i++)
                {
                    if (i == 0 && !includeParent) continue;
                    Batchable batchable = children[i].GetComponent<Batchable>();
                    if (batchable != null) batchable.Unpack();
                }
            }
        }

#endif

        void GetBatchers()
        {
            List<Batchable> batchableList = new List<Batchable>();
            if (includeParent)
            {
                Batchable batchable = GetComponent<Batchable>();
                if (batchable == null) batchable = gameObject.AddComponent<Batchable>();
                batchableList.Add(batchable);
            }
            List<Transform> children = new List<Transform>();
            Dreamteck.SceneUtility.GetChildrenRecursively(transform, ref children);
            for (int i = 1; i < children.Count; i++)
            {
                Batchable batchable = children[i].GetComponent<Batchable>();
                if (batchable != null) batchableList.Add(batchable);
            }
            batchables = batchableList.ToArray();
        }


        protected override IEnumerator BuildAsync()
        {
            if (mode == Mode.Dynamic) GetBatchers();
            if (includeParent)
            {
                Batchable parentBatchable = GetComponent<Batchable>();
                if (parentBatchable == null) parentBatchable = gameObject.AddComponent<Batchable>();
                parentBatchable.UpdateImmediate();
                AddBatcherToQueue(parentBatchable);
            }
            for (int i = 0; i < batchables.Length; i++)
            {
                if (batchables[i] == null) continue;
                if (!batchables[i].gameObject.activeInHierarchy) continue;
                AddBatcherToQueue(batchables[i]);
            }
            combinedMeshes = new Mesh[combineQueues.Count];
            for (int i = 0; i < combineQueues.Count; i++)
            {
                combinedMeshes[i] = combineQueues[i].Combine("Combined " + i);
                yield return null;
            }
            combineQueues.Clear();
        }

        void AddBatcherToQueue(Batchable batchable)
        {
            if (batchable.GetMesh() == null) return;
            bool added = false;
            for (int i = 0; i < combineQueues.Count; i++)
            {
                if (combineQueues[i].Add(batchable))
                {
                    added = true;
                    batchable.DeactivateRenderer();
                    break;
                }
            }

            if (!added)
            {
                batchable.DeactivateRenderer();
                combineQueues.Add(new CombineChildQueue(transform, batchable.GetMaterials()));
                combineQueues[combineQueues.Count - 1].Add(batchable);
            }
        }

        private void Reset()
        {
            priority = 99;
        }

        void OnDestroy()
        {
            for (int i = 0; i < combinedMeshes.Length; i++) Destroy(combinedMeshes[i]);
        }

    }
}
