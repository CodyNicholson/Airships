using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airships.Tests
{
    public class ProjectSettingsTests
    {
        [Test]
        public void BothGraphicsModesExistOnPhones()
        {
            var manager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var levels = new SerializedObject(manager).FindProperty("m_QualitySettings");
            Assert.AreEqual(2, levels.arraySize, "Expected the Low and High levels");

            for (var i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var name = level.FindPropertyRelative("name").stringValue;
                var excluded = level.FindPropertyRelative("excludedTargetPlatforms");
                for (var e = 0; e < excluded.arraySize; e++)
                {
                    var platform = excluded.GetArrayElementAtIndex(e).stringValue;
                    Assert.AreNotEqual("Android", platform, $"{name} is excluded on Android");
                    Assert.AreNotEqual("iPhone", platform, $"{name} is excluded on iOS");
                }
                Assert.IsNotNull(QualitySettings.GetRenderPipelineAssetAt(i) as RenderPipelineAsset, $"{name} has no render pipeline asset");
            }
        }
    }
}
