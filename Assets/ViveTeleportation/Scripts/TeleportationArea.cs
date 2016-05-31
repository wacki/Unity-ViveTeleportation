using UnityEngine;
using System.Collections.Generic;
using System;

namespace Wacki {

    [RequireComponent(typeof(MeshCollider))]
    public class TeleportationArea : MonoBehaviour {

        public string meshSavePath = "Assets/TeleportationAreas/";

        [SerializeField]
        private VertexList _mainMeshVertices;

        [SerializeField]
        private List<VertexList> _meshHoles;

        public VertexList mainMeshVertices
        {
            set { _mainMeshVertices = value; }
            get { return _mainMeshVertices; }
        }
        public List<VertexList> meshHoles
        {
            set { _meshHoles = value; }
            get { return _meshHoles; }
        }


        void TestGenerateMesh()
        {

        }

    }

    [Serializable]
    public class VertexList {
        [SerializeField]
        public List<Vector3> vertices = new List<Vector3>();
    }

}