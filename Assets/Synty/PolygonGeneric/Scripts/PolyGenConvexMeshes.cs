using UnityEngine;
using System.Collections.Generic;

namespace Synty.PolygonGeneric
{
    public class PolyGenConvexMeshes : ScriptableObject
    {
        public List<Mesh> ConvexMeshes;
        public string HashOfSourceMeshes;
    }
}
