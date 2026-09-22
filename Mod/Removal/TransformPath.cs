using UnityEngine;

namespace FarmhouseWardrobeFix
{
    internal static class TransformPath
    {
        internal static string Get(Transform transform)
        {
            if (transform == null)
                return "<null>";

            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
