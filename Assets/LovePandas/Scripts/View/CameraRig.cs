using UnityEngine;

namespace LovePandas.View
{
    /// Плавные ракурсы главной камеры: обычный вид и вид для инвентаря, где панда прижата к правому краю
    /// и чуть крупнее, чтобы дощечка слева её не закрывала.
    public class CameraRig : MonoBehaviour
    {
        Camera cam;
        Vector3 basePos;
        float targetViewportX = 0.5f, targetZoom = 1f;
        float viewportX = 0.5f, zoom = 1f;

        void Awake()
        {
            cam = GetComponent<Camera>();
            basePos = transform.localPosition;
        }

        /// viewportX — где по ширине экрана должна стоять панда (0.5 — по центру); zoom &lt; 1 — ближе.
        public void Focus(float viewportX, float zoom)
        {
            targetViewportX = viewportX;
            targetZoom = zoom;
        }

        public void Reset() => Focus(0.5f, 1f);

        void LateUpdate()
        {
            float k = 1 - Mathf.Exp(-Time.deltaTime * 7f);
            viewportX = Mathf.Lerp(viewportX, targetViewportX, k);
            zoom = Mathf.Lerp(zoom, targetZoom, k);

            // камера на линии панды (x = 0): сдвиг камеры влево на (vx − 0.5)·ширину кадра ставит панду в vx
            float dist = -basePos.z * zoom;
            float halfW = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
            transform.localPosition = new Vector3(-(viewportX - 0.5f) * 2f * halfW, basePos.y, -dist);
        }
    }
}
