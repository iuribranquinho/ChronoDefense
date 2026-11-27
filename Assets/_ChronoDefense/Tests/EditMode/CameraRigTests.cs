// Enquadramento do CameraBounds e trava de pan do CameraRig.
using ChronoDefense.Editor;
using ChronoDefense.UI;
using NUnit.Framework;
using UnityEngine;

namespace ChronoDefense.Tests
{
    public class CameraRigTests
    {
        GameObject _go;
        CameraRig _rig;
        Bounds _bounds;

        [SetUp]
        public void SetUp()
        {
            _bounds = MvpDataGenerator.Layouts[0].Frame;   // fase 1 (Vale Gelado)
            _go = new GameObject("Cam", typeof(Camera));
            _go.GetComponent<Camera>().aspect = 16f / 9f;
            _rig = _go.AddComponent<CameraRig>();
            _rig.Init(_go.GetComponent<Camera>(), _bounds);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_go);

        void Viewport(out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? _bounds.min.x : _bounds.max.x,
                                    (i & 2) == 0 ? _bounds.min.y : _bounds.max.y,
                                    (i & 4) == 0 ? _bounds.min.z : _bounds.max.z);
                Vector3 v = _rig.Cam.WorldToViewportPoint(c);
                Assert.Greater(v.z, 0f, "canto atrás da câmera");
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
        }

        [TestCase(16f / 9f)]
        [TestCase(19.5f / 9f)]
        [TestCase(4f / 3f)]
        public void WholeLayoutFitsSafeViewport_AndFillsIt(float aspect)
        {
            _rig.Cam.aspect = aspect;
            _rig.Refit();
            Viewport(out Vector2 min, out Vector2 max);
            Rect safe = CameraRig.SafeViewport;
            const float eps = 1e-3f;
            Assert.GreaterOrEqual(min.x, safe.xMin - eps);
            Assert.LessOrEqual(max.x, safe.xMax + eps);
            Assert.GreaterOrEqual(min.y, safe.yMin - eps);
            Assert.LessOrEqual(max.y, safe.yMax + eps);
            // Preenche: encosta na região segura em pelo menos um eixo (sem terreno vazio sobrando).
            float slackX = (safe.width - (max.x - min.x)), slackY = (safe.height - (max.y - min.y));
            Assert.Less(Mathf.Min(slackX, slackY), 0.01f, $"sobra x={slackX:0.000} y={slackY:0.000}");
            // Centralizado na região segura.
            Vector2 mid = (min + max) * 0.5f;
            Assert.AreEqual(safe.center.x, mid.x, 0.01f);
            Assert.AreEqual(safe.center.y, mid.y, 0.01f);
        }

        [Test]
        public void PanIsLockedAtCenter_WhenWholeMapVisible()
        {
            Vector3 before = _rig.Cam.transform.position;
            Assert.IsTrue(_rig.PanLocked);
            _rig.PanBy(new Vector3(12f, 0f, -9f));
            Assert.AreEqual(_rig.FittedFocus, _rig.Focus);
            Assert.That(Vector3.Distance(before, _rig.Cam.transform.position), Is.LessThan(1e-4f));

            _rig.SetZoom(1.15f);   // afastar continua travado
            _rig.PanBy(new Vector3(5f, 0f, 5f));
            Assert.AreEqual(_rig.FittedFocus, _rig.Focus);
        }

        [Test]
        public void ZoomingIn_UnlocksOnlyTheOverflow()
        {
            _rig.SetZoom(0.85f);
            Assert.IsFalse(_rig.PanLocked);
            _rig.PanBy(new Vector3(100f, 0f, 100f));
            Vector3 d = _rig.Focus - _rig.FittedFocus;
            Assert.AreEqual(_bounds.extents.x * 0.15f, d.x, 1e-3f);
            Assert.AreEqual(_bounds.extents.z * 0.15f, d.z, 1e-3f);
        }
    }
}
