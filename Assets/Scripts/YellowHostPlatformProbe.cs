using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace CandyCruisers
{
    // Used only by the isolated desktop/phone validation scene, never self-installs.
    public sealed class YellowHostPlatformProbe : MonoBehaviour
    {
        public GameObject[] Prefabs;
        public GameObject Missile;
        public Sprite Shield;
        private EnemyGrid grid;
        private GridEnemy Add(EnemyColor color, int column, int row)
        {
            var instance = Instantiate(Prefabs[(int)color]);
            var enemy = instance.GetComponent<GridEnemy>() ?? instance.AddComponent<GridEnemy>();
            enemy.Configure(color, column, row);
            instance.transform.SetParent(grid.transform, false);
            grid.Register(enemy);
            enemy.GetComponent<EnemyAbilities>().Suspended = true;
            return enemy;
        }
        private static void Tick(EnemyAbilities ability, float seconds)
        {
            ability.Suspended = false;
            ability.Tick(seconds);
            ability.Suspended = true;
        }
        private static void Require(bool value, string message)
        { if (!value) throw new Exception(message); }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            string result;
            try
            {
                int cases = MimicMechanicContract.Run(Prefabs, Missile, Shield);
                grid = new GameObject("Split preview", typeof(EnemyGrid), typeof(EnemyRowSpawner)).GetComponent<EnemyGrid>();
                grid.ConfigureAbilities(Missile, Shield);
                var preview = grid.GetComponent<EnemyRowSpawner>();
                preview.Configure(Prefabs[1], Prefabs[0]);
                preview.ConfigureNewTypes(Prefabs[2], Prefabs[3], Prefabs[4], Prefabs[5]);
                preview.ConfigureSpecialSprite(Prefabs[5].GetComponentInChildren<SpriteRenderer>().sprite);
                grid.transform.position = Vector3.up * 3.6f;
                MimicMechanicContract.PopulateGallery(grid, Add);
                result = "PASS: " + cases + " host cases at 30/60/120 frames per second; " + Application.platform;
            }
            catch (Exception error) { result = "FAIL: " + error; Debug.LogError(result); }
            Debug.Log(result);
            File.WriteAllText(Path.Combine(Application.persistentDataPath, "yellow-host-result.txt"), result);
            yield return new WaitForSecondsRealtime(3);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Application.platform == RuntimePlatform.IPhonePlayer ? "yellow-host-platform.png" :
                Path.Combine(Application.persistentDataPath, "yellow-host-platform.png"));
        }
    }
}
