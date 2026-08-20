using System.IO;
using System.Linq;
using EntropyTag.Presentation;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace EntropyTag.Tests.EditMode
{
    public sealed class PlayerInputAssetTests
    {
        [Test]
        public void GameplayActionsSupportKeyboardMouseAndGamepad()
        {
            string path = Path.Combine(
                UnityEngine.Application.dataPath,
                "EntropyTag",
                "Settings",
                "Input",
                "EntropyTagInputActions.inputactions");
            InputActionAsset asset = InputActionAsset.FromJson(File.ReadAllText(path));

            Assert.That(asset.FindControlSchemeIndex("KeyboardMouse"), Is.GreaterThanOrEqualTo(0));
            Assert.That(asset.FindControlSchemeIndex("Gamepad"), Is.GreaterThanOrEqualTo(0));
            Assert.That(asset.FindAction("Gameplay/Move", true), Is.Not.Null);
            Assert.That(asset.FindAction("Gameplay/Look", true), Is.Not.Null);
            Assert.That(asset.FindAction("Gameplay/Aim", true), Is.Not.Null);
            Assert.That(asset.FindAction("Gameplay/Fire", true), Is.Not.Null);
        }

        [Test]
        public void CameraStudyDefinesEightNamedVariants()
        {
            CameraAimExperimentMode[] modes =
                System.Enum.GetValues(typeof(CameraAimExperimentMode)).Cast<CameraAimExperimentMode>().ToArray();
            string[] names = modes.Select(CameraAimExperimentController.GetDisplayName).ToArray();

            Assert.That(modes, Has.Length.EqualTo(8));
            Assert.That(names.Distinct().Count(), Is.EqualTo(8));
        }
    }
}
