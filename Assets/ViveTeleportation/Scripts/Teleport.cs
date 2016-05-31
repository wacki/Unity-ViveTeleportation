using UnityEngine;

namespace Wacki {

    public class Teleport : MonoBehaviour {
        
        public LayerMask teleportLayers;
        public PlayAreaVis playAreaVisPrefab;

        public Color legalAreaColor = Color.green;
        public Color illegalAreaColor = Color.red;

        private PlayAreaVis _playAreaVis;
        private SteamVR_TrackedController _trackedController;

        Transform reference
        {
            get
            {
                var top = SteamVR_Render.Top();
                return (top != null) ? top.origin : null;
            }
        }

        void Start()
        {
            _trackedController = GetComponent<SteamVR_TrackedController>();
            if(_trackedController == null) {
                _trackedController = gameObject.AddComponent<SteamVR_TrackedController>();
            }

            _trackedController.PadUnclicked += new ClickedEventHandler(DoClick);

            _playAreaVis = Instantiate(playAreaVisPrefab);
            _playAreaVis.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            Destroy(_playAreaVis);
        }
        
        void Update()
        {
            if(_trackedController.padPressed)
                UpdateTargetLocation();
            else
                _playAreaVis.gameObject.SetActive(false);
        }

        void UpdateTargetLocation(bool teleport = false)
        {
            var t = reference;
            if(t == null)
                return;

            float refY = t.position.y;
            Plane groundPlane = new Plane(Vector3.up, -refY);

            Ray ray = new Ray(this.transform.position, transform.forward);


            RaycastHit hitInfo;
            bool legalArea = Physics.Raycast(ray, out hitInfo, 10.0f, teleportLayers);
            float dist = hitInfo.distance;

            // for now just display the illegal area teleport by using the current ground plane (not so nice)
            if(!legalArea)
                groundPlane.Raycast(ray, out dist);

            if(dist > 0) {
                _playAreaVis.gameObject.SetActive(true);
                _playAreaVis.transform.position = transform.position + dist * transform.forward;

                if(legalArea) {
                    _playAreaVis.SetColor(legalAreaColor);

                    if(teleport)
                        t.position = transform.position + dist * transform.forward;

                }
                else
                    _playAreaVis.SetColor(illegalAreaColor);

            }
            else {
                _playAreaVis.gameObject.SetActive(false);
            }
        }

        void DoClick(object sender, ClickedEventArgs e)
        {
            // make sure our target is updated
            UpdateTargetLocation(true);
        }
    }

}