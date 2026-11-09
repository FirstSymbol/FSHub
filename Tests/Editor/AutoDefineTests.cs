using NUnit.Framework;

namespace FirstSymbol.Hub.Tests
{
    public class AutoDefineTests
    {
        [Test]
        public void ADefineIsAddedWhenWantedAndMissing()
        {
            Assert.AreEqual(AutoDefine.Step.Add, AutoDefine.Plan(true, false, false));
        }

        [Test]
        public void ADefineTheUserSetIsNeverTouched()
        {
            Assert.AreEqual(AutoDefine.Step.None, AutoDefine.Plan(true, true, false));
            Assert.AreEqual(AutoDefine.Step.None, AutoDefine.Plan(false, true, false));
            Assert.AreEqual(AutoDefine.Step.None, AutoDefine.Plan(false, false, false));
        }

        [Test]
        public void OnlyWhatTheToolAddedIsTakenAway()
        {
            Assert.AreEqual(AutoDefine.Step.Remove, AutoDefine.Plan(false, true, true));
            Assert.AreEqual(AutoDefine.Step.Forget, AutoDefine.Plan(false, false, true));
        }

        [Test]
        public void AHubFolderUnderAssetsWantsTheSymbolAndAPackageDoesNot()
        {
            Assert.IsTrue(AutoDefine.Wanted("Assets/Plugins/FSHub/Editor/FirstSymbol.Hub.Editor.asmdef"));
            Assert.IsFalse(AutoDefine.Wanted("Packages/com.firstsymbol.hub/Editor/FirstSymbol.Hub.Editor.asmdef"));
            Assert.IsFalse(AutoDefine.Wanted(null));
        }
    }
}
