using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Pathfinding.Poly2Tri;
using UnityEditorInternal;
using UnityEditor.SceneManagement;

namespace Wacki {

    [CustomEditor(typeof(TeleportationArea))]
    public class TeleportationAreaEditor : Editor {

        protected TeleportationArea script { get { return (TeleportationArea)target; } }

        int selectedHandle;
        float lastTriangulation = 0.0f;

        private SerializedProperty _meshSavePathProp;
        private SerializedProperty _meshVerticesProp;
        private SerializedProperty _meshHolesProp;
        private List<SerializedProperty> _meshHolesPropElements;

        private ReorderableList meshVertices;
        private List<ReorderableList> meshHoles;

        void OnEnable()
        {
            // set serialized properties
            _meshSavePathProp = serializedObject.FindProperty("meshSavePath");
            _meshVerticesProp = serializedObject.FindProperty("_mainMeshVertices");
            _meshHolesProp = serializedObject.FindProperty("_meshHoles");

            // fill in the _meshHolesPropElements with the arrays serializedproperties
            _meshHolesPropElements = new List<SerializedProperty>();
            for(int i = 0; i < _meshHolesProp.arraySize; ++i) {
                _meshHolesPropElements.Add(_meshHolesProp.GetArrayElementAtIndex(i));
            }

            // initialize reorderable lists
            meshVertices = CreateReorderableList(serializedObject, _meshVerticesProp.FindPropertyRelative("vertices"));


            meshHoles = new List<ReorderableList>();
            foreach(var elem in _meshHolesPropElements)
                meshHoles.Add(CreateReorderableList(serializedObject, elem.FindPropertyRelative("vertices")));

            EditorApplication.update += Update;
        }

        void OnDisable()
        {
            EditorApplication.update -= Update;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_meshSavePathProp);

            if(DrawPropertyFoldout(_meshVerticesProp)) {
                meshVertices.DoLayoutList();
            }

            if(DrawPropertyFoldout(_meshHolesProp)) {
                EditorGUI.indentLevel = 1;
                for(int i = 0; i < _meshHolesPropElements.Count; ++i) {

                    // Draw foldout header including remove button for each hole
                    GUILayout.BeginHorizontal();
                    DrawPropertyFoldout(_meshHolesPropElements[i], "Hole " + i);

                    if(GUILayout.Button("Remove hole")) {
                        _meshHolesPropElements.RemoveAt(i);
                        meshHoles.RemoveAt(i);
                        script.meshHoles.RemoveAt(i);
                        EditorUtility.SetDirty(target);
                        serializedObject.Update();
                        OnEnable();

                        break;
                    }
                    GUILayout.EndHorizontal();

                    if(_meshHolesPropElements[i].isExpanded) {

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.BeginVertical();
                        meshHoles[i].DoLayoutList();
                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                    }

                }
            }

            if(GUILayout.Button("Add Hole")) {
                AddMeshHole();
            }

            if(GUILayout.Button("Generate")) {
                Triangulate();
            }

            serializedObject.ApplyModifiedProperties();
        }

        void AddMeshHole()
        {
            script.meshHoles.Add(new VertexList());
            serializedObject.Update();
            OnEnable();
        }

        bool DrawPropertyFoldout(SerializedProperty prop, string name = null)
        {
            if(name == null)
                name = prop.displayName;

            // we use the non layouted foldout because it has the toggleOnLabelClick argument
            prop.isExpanded = EditorGUI.Foldout(EditorGUILayout.GetControlRect(), prop.isExpanded, name, true);
            return prop.isExpanded;
        }

        void Triangulate()
        {
            // fill up the poly2tri container that holds our main mesh vertices
            List<PolygonPoint> points = new List<PolygonPoint>();

            // fill them with our data        
            foreach(var point in script.mainMeshVertices.vertices) {
                points.Add(new PolygonPoint(point.x, point.z));
            }
            Polygon pointSet = new Polygon(points);

            // add our holes to the pointSet variable        
            foreach(var hole in script.meshHoles) {

                var holeVerts = hole.vertices;
                if(holeVerts.Count >= 3) {
                    List<PolygonPoint> pointsHole = new List<PolygonPoint>();
                    foreach(var point in holeVerts) {
                        pointsHole.Add(new PolygonPoint(point.x, point.z));
                    }
                    pointSet.AddHole(new Polygon(pointsHole));
                }
            }

            P2T.Triangulate(pointSet);

            // to generate correct vertex indices for our triangulation we add all of the triangulation points into a single list ...
            List<TriangulationPoint> triangulationVertices = new List<TriangulationPoint>();
            triangulationVertices.AddRange(pointSet.Points);

            // ... including the holes
            if(pointSet.Holes != null) {
                foreach(var holePoints in pointSet.Holes) {
                    foreach(var point in holePoints.Points) {
                        triangulationVertices.Add(point);
                    }
                }
            }

            // create mesh
            MeshCollider mc = script.GetComponent<MeshCollider>();
            // remove old mesh if it exists
            DeleteMesh(mc.sharedMesh);

            Mesh mesh = new Mesh();
            mesh.name = "TeleportationAreaMesh_" + GetHashCode();

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            foreach(var point in triangulationVertices) {
                vertices.Add(new Vector3(point.Xf, 0.0f, point.Yf));
            }

            foreach(var tri in pointSet.Triangles) {
                triangles.Add(triangulationVertices.IndexOf(tri.Points[2]));
                triangles.Add(triangulationVertices.IndexOf(tri.Points[1]));
                triangles.Add(triangulationVertices.IndexOf(tri.Points[0]));
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();

            SaveMesh(mesh);
            mc.sharedMesh = mesh;
            EditorUtility.SetDirty(mc);

            // the SetDirty above doesn't do anything
            // our link to the just created mesh will be lost
            // when we switch scenes so we use this ugly workaround
            // below for now...
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

        }

        void DeleteMesh(Mesh mesh)
        {
            if(mesh == null)
                return;

            if(AssetDatabase.Contains(mesh)) {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(mesh));
            }
        }

        void SaveMesh(Mesh mesh, bool optimizeMesh = true)
        {
            MeshFilter mf = script.GetComponent<MeshFilter>();

            // todo: makse sure we don't override an existing mesh with this
            // and also make sure we don't leave behind unused meshes
            //string path = EditorUtility.SaveFilePanel("Save Separate Mesh Asset", "Assets/", mesh.name, "asset");
            //path = FileUtil.GetProjectRelativePath(path);

            string path = script.meshSavePath + mesh.name + ".asset";
            if(string.IsNullOrEmpty(path)) return;

            EnsurePathExists(script.meshSavePath);

            if(optimizeMesh)
                mesh.Optimize();

            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
        }

        void EnsurePathExists(string path)
        {
            string[] pathExploded = path.Split('/');
            string parentFolder = "";
            foreach(string folder in pathExploded) {
                if(string.IsNullOrEmpty(folder)) continue;

                if(!AssetDatabase.IsValidFolder(parentFolder + folder)) {
                    AssetDatabase.CreateFolder(parentFolder.TrimEnd('/'), folder);
                    Debug.Log("CreateFolder " + parentFolder.TrimEnd('/') + ", " + folder);
                    Debug.Log(parentFolder + folder);
                }

                parentFolder += folder + "/";
            }
        }

        void Update()
        {
            //    lastTriangulation += 0.05f;
            //    if(lastTriangulation > 1.0f) {
            //        Triangulate();
            //        lastTriangulation = 0.0f;

            //    }
        }

        void OnSceneGUI()
        {


            Vector3 parentPos = script.transform.position;
            //Debug.Log(Event.current.type + " " + GUIUtility.hotControl + " " );

            Handles.color = Color.green;
            var list = script.mainMeshVertices.vertices;
            if(list.Count > 0) {
                Vector3 prev = list[list.Count - 1] + parentPos; // last element
                for(int i = 0; i < list.Count; ++i) {
                    var point = list[i] + parentPos;

                    //Handles.SphereCap(EditorGUIUtility.GetControlID(FocusType.Passive), point, Quaternion.identity, 0.1f);
                    Vector3 newPosition = Handles.FreeMoveHandle(point, Quaternion.identity, 0.1f, new Vector3(1, 1, 1), Handles.SphereCap);
                    list[i] = new Vector3(newPosition.x, point.y, newPosition.z) - parentPos;

                    Handles.DrawLine(prev, point);
                    prev = point;
                }
            }


            Handles.color = Color.red;
            var holeList = script.meshHoles;
            foreach(var hole in holeList) {
                var holeVerts = hole.vertices;
                if(holeVerts.Count > 0) {
                    Vector3 prev = holeVerts[holeVerts.Count - 1] + parentPos; // last element
                    for(int i = 0; i < holeVerts.Count; ++i) {
                        var point = holeVerts[i] + parentPos;

                        Vector3 newPosition = Handles.FreeMoveHandle(point, Quaternion.identity, 0.1f, new Vector3(1, 1, 1), Handles.SphereCap);
                        holeVerts[i] = new Vector3(newPosition.x, point.y, newPosition.z) - parentPos;

                        Handles.DrawLine(prev, point);
                        prev = point;
                    }
                }
            }



            //Handles.FreeMoveHandle()
        }


        ReorderableList CreateReorderableList(SerializedObject obj, SerializedProperty prop, string name = null)
        {
            ReorderableList list = new ReorderableList(obj, prop, true, true, true, true);

            if(name == null)
                name = prop.displayName;

            list.drawHeaderCallback = rect => {
                EditorGUI.LabelField(rect, name);
            };


            list.drawElementCallback = (rect, index, active, focused) => {
                SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
                element.vector3Value = EditorGUI.Vector3Field(rect, "Position " + index, element.vector3Value);
            };

            //list.elementHeightCallback = (index) => {
            //};

            return list;
        }
    }

}