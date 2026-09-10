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
            Assert.That(asset.FindAction("Gameplay/Jump", true), Is.Not.Null);
            Assert.That(asset.FindAction("Gameplay/Slide", true), Is.Not.Null);
            Assert.That(asset.FindAction("Gameplay/SwitchElement", true), Is.Not.Null);
            Assert.That(asset.FindAction("Gameplay/ResetTerritory", true), Is.Not.Null);
            Assert.That(asset.FindAction("Gameplay/ConfirmMatch", true), Is.Not.Null);
            Assert.That(
                asset.FindAction("Gameplay/ConfirmMatch", true).bindings.Any(binding => binding.path == "<Keyboard>/enter"),
                Is.True);
            Assert.That(
                asset.FindAction("Gameplay/ConfirmMatch", true).bindings.Any(binding => binding.path == "<Gamepad>/start"),
                Is.True);
            Assert.That(
                asset.FindAction("Gameplay/Jump", true).bindings.Any(binding => binding.path == "<Keyboard>/space"),
                Is.True);
            Assert.That(
                asset.FindAction("Gameplay/Jump", true).bindings.Any(binding => binding.path == "<Gamepad>/buttonSouth"),
                Is.True);
            Assert.That(
                asset.FindAction("Gameplay/Slide", true).bindings.Any(binding => binding.path == "<Keyboard>/leftCtrl"),
                Is.True);
            Assert.That(
                asset.FindAction("Gameplay/Slide", true).bindings.Any(binding => binding.path == "<Gamepad>/buttonEast"),
                Is.True);
            Assert.That(
                asset.FindAction("Gameplay/SwitchElement", true).bindings.Any(binding => binding.path == "<Keyboard>/tab"),
                Is.True);
            Assert.That(
                asset.FindAction("Gameplay/SwitchElement", true).bindings.Any(binding => binding.path == "<Gamepad>/buttonNorth"),
                Is.True);
            Assert.That(
                asset.FindAction("Gameplay/ResetTerritory", true).bindings.Any(binding => binding.path == "<Keyboard>/r"),
                Is.True);
            Assert.That(
                asset.FindAction("Gameplay/ResetTerritory", true).bindings.Any(binding => binding.path == "<Gamepad>/select"),
                Is.True);
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
