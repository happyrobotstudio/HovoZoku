using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dreamteck.Forever
{
    [AddComponentMenu("Dreamteck/Forever/Resource Management")]
    public class ResourceManagement : MonoBehaviour, ISerializationCallbackReceiver
    {
        [Tooltip("List of objects to protect from unloading. Adding prefabs to the list will protect their related meshes, materials, textures and audioclips.")]
        public Object[] persistentObjects = new Object[0];

        [HideInInspector]
        [SerializeField]
        private Object[] persistentResources = new Object[0];

        private static ResourceManagement instance = null;

        private static List<UnloadableResource> unloadableResources = new List<UnloadableResource>();
        private static bool active = false;

        void Awake()
        {
            instance = this;
            active = true;
        }

        private void OnDestroy()
        {
            active = false;
        }

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            List<Object> unique = new List<Object>();
            for (int i = 0; i < persistentObjects.Length; i++)
            {
                if (persistentObjects[i] is Material || persistentObjects[i] is Mesh || persistentObjects[i] is Texture || persistentObjects[i] is AudioClip) AddIfUnique(persistentObjects[i], unique);
                else if (persistentObjects[i] is GameObject) ExtractUniqueResources(persistentObjects[i] as GameObject, unique);
                if (persistentObjects[i] is Material)
                {
                    Texture[] tex = GetTexturesFromMaterial(persistentObjects[i] as Material);
                    for (int j = 0; j < tex.Length; j++) AddIfUnique(tex[j], unique);
                }
            }
            persistentResources = unique.ToArray();
#endif
        }

        public void OnAfterDeserialize()
        {
        }


        private static void AddIfUnique(Object obj, List<Object> list)
        {
            if (obj == null) return;
            if (!list.Contains(obj)) list.Add(obj);
        }


        public static void RegisterUnloadableResource(Object obj, int segmentIndex)
        {
            if (!active) return;
            if (obj == null) return;
            for (int i = 0; i < unloadableResources.Count; i++)
            {
                if (unloadableResources[i].resource == obj)
                {
                    unloadableResources[i].segmentIndex = segmentIndex;
                    return;
                }
            }
            unloadableResources.Add(new UnloadableResource(obj, segmentIndex));
        }

        public static void UnRegisterUnloadableResource(Object obj)
        {
            if (!active) return;
            if (obj == null) return;
            for (int i = 0; i < unloadableResources.Count; i++)
            {
                if (unloadableResources[i].resource == obj)
                {
                    unloadableResources.RemoveAt(i);
                    return;
                }
            }
        }

        public static void UnRegisterUnloadableResources(int segmentIndex)
        {
            if (!active) return;
            for (int i = 0; i < unloadableResources.Count; i++)
            {
                if (unloadableResources[i].segmentIndex == segmentIndex)
                {
                    unloadableResources.RemoveAt(i);
                    i--;
                    continue;
                }
            }
        }

        public static void UnloadResources(int segmentIndex)
        {
            if (!active) return;
            if (LevelGenerator.instance.testMode) return;
            for (int i = unloadableResources.Count - 1; i >= 0; i--)
            {
                if (unloadableResources[i].segmentIndex <= segmentIndex)
                {
                    if (IsConsistent(unloadableResources[i].resource)) continue;
                    Resources.UnloadAsset(unloadableResources[i].resource);
                    unloadableResources.RemoveAt(i);
                }
            }
        }

        public static void UnloadResources()
        {
            if (!active) return;
            if (LevelGenerator.instance.testMode) return;
            for (int i = 0; i < unloadableResources.Count; i++)
            {
                if (IsConsistent(unloadableResources[i].resource)) continue;
                Resources.UnloadAsset(unloadableResources[i].resource);
            }
            unloadableResources.Clear();
        }

        static bool IsConsistent(Object resource)
        {
            for (int i = 0; i < instance.persistentResources.Length; i++)
            {
                if (instance.persistentResources[i] == resource) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        public static void ExtractUniqueResources(GameObject go, List<Object> resources) //Call this in the editor when serializing instead of during the game
        {
            MeshFilter filter = go.GetComponent<MeshFilter>();
            Renderer rend = go.GetComponent<Renderer>();
            AudioSource audio = go.GetComponent<AudioSource>();
            MeshCollider collider = go.GetComponent<MeshCollider>();
            if (filter != null) AddIfUnique(filter.sharedMesh, resources);
            if (rend != null)
            {
                for (int j = 0; j < rend.sharedMaterials.Length; j++)
                {
                    if (rend.sharedMaterials[j] != null)
                    {
                        AddIfUnique(rend.sharedMaterials[j], resources);
                        Texture[] tex = GetTexturesFromMaterial(rend.sharedMaterials[j]);
                        for (int k = 0; k < tex.Length; k++) AddIfUnique(tex[k], resources);
                    }
                }
                if (rend is ParticleSystemRenderer)
                {
                    ParticleSystemRenderer psrend = (ParticleSystemRenderer)rend;
                    Mesh[] psMeshes = new Mesh[4];
                    int meshCount = psrend.GetMeshes(psMeshes);
                    for (int j = 0; j < meshCount; j++) AddIfUnique(psMeshes[j], resources);
                }
            }
            if (audio != null && audio.clip != null && !resources.Contains(audio.clip)) resources.Add(audio.clip);
            if (collider != null && collider.sharedMesh != null && !resources.Contains(collider.sharedMesh)) resources.Add(collider.sharedMesh);
        }

        static Texture[] GetTexturesFromMaterial(Material material)
        {
            Shader shader = material.shader;
            Texture[] textures = new Texture[UnityEditor.ShaderUtil.GetPropertyCount(shader)];
            for (int i = 0; i < textures.Length; i++)
            {
                if (UnityEditor.ShaderUtil.GetPropertyType(shader, i) == UnityEditor.ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    textures[i] = material.GetTexture(UnityEditor.ShaderUtil.GetPropertyName(shader, i));
                }
            }
            return textures;
        }
#endif

        internal class UnloadableResource
        {
            internal Object resource = null;
            internal int segmentIndex = 0;

            internal UnloadableResource(Object obj, int indeX)
            {
                resource = obj;
                segmentIndex = indeX;
            }
        }
    }
}
