using UnityEngine;
using System.Collections;
using Valve.VR;

namespace Wacki {

    [ExecuteInEditMode, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PlayAreaVis : MonoBehaviour {

        public int playerSlices = 16;
        public float playerRadius = 0.2f;
        public float playerBorderHeight = 0.1f;
        public float borderThickness = 0.15f;
        public Color color = Color.cyan;

        public enum Size {
            Calibrated,
            _400x300,
            _300x225,
            _200x150
        }

        public Size playAreaSize;

        [SerializeField, HideInInspector]
        private GameObject _playerObject;

        [SerializeField, HideInInspector]
        private Material _material = null;

        public static bool GetBounds(Size size, ref HmdQuad_t pRect)
        {
            if(size == Size.Calibrated) {
                var initOpenVR = (!SteamVR.active && !SteamVR.usingNativeSupport);
                if(initOpenVR) {
                    var error = EVRInitError.None;
                    OpenVR.Init(ref error, EVRApplicationType.VRApplication_Other);
                }

                var chaperone = OpenVR.Chaperone;
                bool success = (chaperone != null) && chaperone.GetPlayAreaRect(ref pRect);
                if(!success)
                    Debug.LogWarning("Failed to get Calibrated Play Area bounds!  Make sure you have tracking first, and that your space is calibrated.");

                if(initOpenVR)
                    OpenVR.Shutdown();

                return success;
            }
            else {
                try {
                    var str = size.ToString().Substring(1);
                    var arr = str.Split(new char[] { 'x' }, 2);

                    // convert to half size in meters (from cm)
                    var x = float.Parse(arr[0]) / 200;
                    var z = float.Parse(arr[1]) / 200;

                    pRect.vCorners0.v0 = x;
                    pRect.vCorners0.v1 = 0;
                    pRect.vCorners0.v2 = z;

                    pRect.vCorners1.v0 = x;
                    pRect.vCorners1.v1 = 0;
                    pRect.vCorners1.v2 = -z;

                    pRect.vCorners2.v0 = -x;
                    pRect.vCorners2.v1 = 0;
                    pRect.vCorners2.v2 = -z;

                    pRect.vCorners3.v0 = -x;
                    pRect.vCorners3.v1 = 0;
                    pRect.vCorners3.v2 = z;

                    return true;
                }
                catch { }
            }

            return false;
        }

        public void BuildMaterial()
        {
            if(_material != null)
                return;

#if UNITY_EDITOR && !(UNITY_5_3 || UNITY_5_2 || UNITY_5_1 || UNITY_5_0)
		_material = new Material(UnityEditor.AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat"));
#else
            _material = new Material(Resources.GetBuiltinResource<Material>("Sprites-Default.mat"));
#endif
            SetColor(color);
        }

        public void BuildAreaMesh()
        {
            var rect = new HmdQuad_t();
            if(!GetBounds(playAreaSize, ref rect))
                return;

            var corners = new HmdVector3_t[] { rect.vCorners0, rect.vCorners1, rect.vCorners2, rect.vCorners3 };

            Vector3[] vertices = new Vector3[corners.Length * 2];
            for(int i = 0; i < corners.Length; i++) {
                var c = corners[i];
                vertices[i] = new Vector3(c.v0, 0.01f, c.v2);
            }

            if(borderThickness == 0.0f) {
                GetComponent<MeshFilter>().mesh = null;
                return;
            }

            for(int i = 0; i < corners.Length; i++) {
                int next = (i + 1) % corners.Length;
                int prev = (i + corners.Length - 1) % corners.Length;

                var nextSegment = (vertices[next] - vertices[i]).normalized;
                var prevSegment = (vertices[prev] - vertices[i]).normalized;

                var vert = vertices[i];
                vert += Vector3.Cross(nextSegment, Vector3.up) * borderThickness;
                vert += Vector3.Cross(prevSegment, Vector3.down) * borderThickness;

                vertices[corners.Length + i] = vert;
            }

            var triangles = new int[]
            {
            0, 1, 4,
            1, 5, 4,
            1, 2, 5,
            2, 6, 5,
            2, 3, 6,
            3, 7, 6,
            3, 0, 7,
            0, 4, 7
            };

            var uv = new Vector2[]
            {
            new Vector2(0.0f, 0.0f),
            new Vector2(1.0f, 0.0f),
            new Vector2(0.0f, 0.0f),
            new Vector2(1.0f, 0.0f),
            new Vector2(0.0f, 1.0f),
            new Vector2(1.0f, 1.0f),
            new Vector2(0.0f, 1.0f),
            new Vector2(1.0f, 1.0f)
            };

            var colors = new Color[]
            {
            Color.white,
            Color.white,
            Color.white,
            Color.white,
            new Color(1.0f, 1.0f, 1.0f, 0.0f),
            new Color(1.0f, 1.0f, 1.0f, 0.0f),
            new Color(1.0f, 1.0f, 1.0f, 0.0f),
            new Color(1.0f, 1.0f, 1.0f, 0.0f)
            };

            var mesh = new Mesh();
            GetComponent<MeshFilter>().mesh = mesh;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = triangles;

            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
#if !(UNITY_5_3 || UNITY_5_2 || UNITY_5_1 || UNITY_5_0)
		renderer.lightProbeUsage = LightProbeUsage.Off;
#else
            renderer.useLightProbes = false;
#endif
        }


        void BuildPlayerMesh()
        {
            if(_playerObject == null) {
                _playerObject = new GameObject("PlayerObject");
                _playerObject.transform.SetParent(transform, false);
            }

            MeshFilter mf = _playerObject.GetComponent<MeshFilter>();
            MeshRenderer renderer = _playerObject.GetComponent<MeshRenderer>();

            if(mf == null)
                mf = _playerObject.AddComponent<MeshFilter>();
            if(renderer == null)
                renderer = _playerObject.AddComponent<MeshRenderer>();


            renderer.material = _material;

            transform.SetParent(transform, false);

            Mesh mesh = new Mesh();

            int numVertices = playerSlices * 2;
            int numIndices = playerSlices * 6;

            Vector3[] vertices = new Vector3[numVertices];
            Color[] colors = new Color[numVertices];
            int[] triangles = new int[numIndices];


            float segmentSizeRad = Mathf.PI * 2.0f / (float)playerSlices;
            float currentAngle = 0.0f;
            for(int i = 0; i < playerSlices; ++i, currentAngle += segmentSizeRad) {

                int i0 = i * 2;
                int i1 = i0 + 1;
                int i2 = (i0 + 2) % numVertices;
                int i3 = (i0 + 3) % numVertices;

                float x = playerRadius * Mathf.Sin(currentAngle);
                float z = playerRadius * Mathf.Cos(currentAngle);

                // vertices
                vertices[i0] = new Vector3(x, 0.0f, z);
                vertices[i1] = new Vector3(x, playerBorderHeight, z);

                // colors
                colors[i0] = Color.white;
                colors[i1] = new Color(1.0f, 1.0f, 1.0f, 0.0f);

                // indices
                if(i < playerSlices) {
                    int triIndex = i * 6;

                    triangles[triIndex] = i0;
                    triangles[triIndex + 1] = i1;
                    triangles[triIndex + 2] = i2;

                    triangles[triIndex + 3] = i2;
                    triangles[triIndex + 4] = i1;
                    triangles[triIndex + 5] = i3;
                }
            }

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;

            mf.sharedMesh = mesh;

            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
#if !(UNITY_5_3 || UNITY_5_2 || UNITY_5_1 || UNITY_5_0)
		renderer.lightProbeUsage = LightProbeUsage.Off;
#else
            renderer.useLightProbes = false;
#endif
        }


        Hashtable values;
        void Update()
        {

#if UNITY_EDITOR
            if(!Application.isPlaying) {

                // build material
                if(_material == null)
                    BuildMaterial();

                // build player mesh
                if(_playerObject == null || _playerObject.GetComponent<MeshFilter>().sharedMesh == null)
                    BuildPlayerMesh();

                // build play area mesh
                var fields = GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

                bool rebuild = false;

                if(values == null || (borderThickness != 0.0f && GetComponent<MeshFilter>().sharedMesh == null)) {
                    rebuild = true;
                }
                else {
                    foreach(var f in fields) {
                        if(!values.Contains(f) || !f.GetValue(this).Equals(values[f])) {
                            rebuild = true;
                            break;
                        }
                    }
                }

                if(rebuild) {
                    BuildAreaMesh();

                    values = new Hashtable();
                    foreach(var f in fields)
                        values[f] = f.GetValue(this);
                }
            }
#endif

            if(_playerObject == null)
                return;


            // move player object
            var top = SteamVR_Render.Top();
            if(top == null)
                return;

            Vector3 headPosOnGround = new Vector3(top.head.localPosition.x, 0.0f, top.head.localPosition.z);
            _playerObject.transform.localPosition = headPosOnGround;
        }


        public void OnEnable()
        {
            if(Application.isPlaying) {
                // If we want the configured bounds of the user,
                // we need to wait for tracking.
                if(playAreaSize == Size.Calibrated)
                    StartCoroutine("UpdateBounds");
            }
        }

        IEnumerator UpdateBounds()
        {
            GetComponent<MeshFilter>().mesh = null; // clear existing

            var chaperone = OpenVR.Chaperone;
            while(chaperone == null) {
                chaperone = OpenVR.Chaperone;
                yield return null;
            }
            while(chaperone.GetCalibrationState() != ChaperoneCalibrationState.OK)
                yield return null;

            BuildMaterial();
            BuildAreaMesh();
            BuildPlayerMesh();
        }


        public void SetColor(Color col)
        {
            _material.SetColor("_Color", col);
        }
    }

}
