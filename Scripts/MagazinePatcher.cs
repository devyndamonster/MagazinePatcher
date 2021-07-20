using Deli.Newtonsoft.Json;
using Deli.Newtonsoft.Json.Converters;
using Deli.Runtime;
using Deli.Setup;
using FistVR;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MagazinePatcher
{
    public class MagazinePatcher : DeliBehaviour
    {
        private static string FolderPath;
        private static string LastTouchedItem;

        private void Awake()
        {
            SetupOutputDirectory();

            PatchLogger.Init();

            Stages.Runtime += OnRuntime;
        }

        private void OnRuntime(RuntimeStage runtime)
        {
            PatchLogger.Log("MagazinePatcher runtime has started!", PatchLogger.LogType.General);
            AnvilManager.Instance.StartCoroutine(LoadMagazineCacheAsync());
        }


        private void SetupOutputDirectory()
        {
            FolderPath = Application.dataPath.Replace("/h3vr_Data", "/BepInEx/plugins/Devyndamonster-MagazinePatcher");

            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }
        }

        


        private static Dictionary<string, MagazineBlacklistEntry> GetMagazineCacheBlacklist()
        {
            Dictionary<string, MagazineBlacklistEntry> blacklist = new Dictionary<string, MagazineBlacklistEntry>();

            try
            {
                string path = FolderPath + "/MagazineCacheBlacklist.json";

                //If the magazine blacklist file does not exist, we'll create a new sample one
                if (!File.Exists(path))
                {
                    StreamWriter sw = File.CreateText(path);
                    List<MagazineBlacklistEntry> blacklistSerialized = new List<MagazineBlacklistEntry>();

                    MagazineBlacklistEntry sample = new MagazineBlacklistEntry();
                    sample.FirearmID = "SKSClassic";
                    sample.MagazineBlacklist.Add("MagazineSKSModern10rnd");
                    sample.MagazineBlacklist.Add("MagazineSKSModern20rnd");

                    blacklistSerialized.Add(sample);

                    string blacklistString = JsonConvert.SerializeObject(blacklistSerialized, Formatting.Indented, new StringEnumConverter());
                    sw.WriteLine(blacklistString);
                    sw.Close();

                    foreach (MagazineBlacklistEntry entry in blacklistSerialized)
                    {
                        blacklist.Add(entry.FirearmID, entry);
                    }
                }

                //If the file does exist, we'll try to deserialize it
                else
                {
                    string blacklistString = File.ReadAllText(path);
                    List<MagazineBlacklistEntry> blacklistDeserialized = JsonConvert.DeserializeObject<List<MagazineBlacklistEntry>>(blacklistString);

                    foreach (MagazineBlacklistEntry entry in blacklistDeserialized)
                    {
                        blacklist.Add(entry.FirearmID, entry);
                    }
                }
            }

            catch (Exception ex)
            {
                //TODO print something
            }

            return blacklist;
        }


        private static void PokeOtherLoader()
        {
            OtherLoader.LoaderStatus.GetLoaderProgress();
        }


        private static IEnumerator LoadMagazineCacheAsync()
        {
            Debug.Log("Patch wait");
            yield return new WaitForSeconds(5);

            PatchLogger.Log("Patching has started", PatchLogger.LogType.General);

            bool canCache = false;
            bool isOtherloaderLoaded = false;

            try
            {
                PokeOtherLoader();
                isOtherloaderLoaded = true;
                PatchLogger.Log("Otherloader detected!", PatchLogger.LogType.General);
            }
            catch
            {
                isOtherloaderLoaded = false;
                PatchLogger.Log("Otherloader not detected!", PatchLogger.LogType.General);
            }

            do
            {
                yield return null;

                if (isOtherloaderLoaded)
                {
                    canCache = OtherLoader.LoaderStatus.GetLoaderProgress() >= 1;
                }

            } while (!canCache && isOtherloaderLoaded);


            CompatibleMagazineCache magazineCache = null;
            Dictionary<string, MagazineBlacklistEntry> blacklist = GetMagazineCacheBlacklist();

            bool isCacheValid = false;
            string cachePath = FolderPath + "/CachedCompatibleMags.json";

            //If the cache exists, we load it and check it's validity
            if (File.Exists(cachePath))
            {
                try
                {
                    string cacheJson = File.ReadAllText(cachePath);
                    magazineCache = JsonConvert.DeserializeObject<CompatibleMagazineCache>(cacheJson);

                    isCacheValid = IsMagazineCacheValid(magazineCache);

                    PatchLogger.Log("Cache file found! Is Valid? " + isCacheValid, PatchLogger.LogType.General);
                }
                catch(Exception e)
                {
                    magazineCache = new CompatibleMagazineCache();

                    PatchLogger.LogError("Failed to read cache file!");
                    PatchLogger.LogError(e.ToString());
                }
            }

            else
            {
                PatchLogger.Log("Cache file not found!", PatchLogger.LogType.General);
                magazineCache = new CompatibleMagazineCache();
            }



            //If the magazine cache file didn't exist, or wasn't valid, we must build a new one
            if (!isCacheValid)
            {
                PatchLogger.Log("Building new magazine cache -- This may take a while!", PatchLogger.LogType.General);

                //Create lists of each category of item
                List<FVRObject> magazines = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Magazine];
                List<FVRObject> clips = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Clip];
                List<FVRObject> bullets = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Cartridge];
                List<FVRObject> firearms = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Firearm];
                AnvilCallback<GameObject> gameObjectCallback;
                int totalObjects = magazines.Count + clips.Count + bullets.Count + firearms.Count;
                int progress = 0;
                DateTime start = DateTime.Now;


                //Loop through all magazines and build a list of magazine components
                PatchLogger.Log("Loading all magazines", PatchLogger.LogType.General);
                foreach (FVRObject magazine in magazines)
                {
                    if ((DateTime.Now - start).TotalSeconds > 2)
                    {
                        start = DateTime.Now;
                        PatchLogger.Log("-- " + ((int)(((float)progress) / totalObjects * 100)) + "% --", PatchLogger.LogType.General);
                    }
                    PatcherStatus.UpdateProgress(Mathf.Min((float)progress / totalObjects, 0.95f));
                    progress += 1;

                    LastTouchedItem = magazine.ItemID;

                    //If this magazine isn't cached, then we should store it's data
                    if (!magazineCache.Magazines.Contains(magazine.ItemID))
                    {
                        gameObjectCallback = magazine.GetGameObjectAsync();
                        Debug.Log("Start: " + magazine.ItemID);
                        yield return AnvilManager.Instance.RunDriven(gameObjectCallback);
                        Debug.Log("End: " + magazine.ItemID);
                        if (gameObjectCallback.Result == null) PatchLogger.LogError("No object was found to use FVRObject! ItemID: " + magazine.ItemID);

                        FVRFireArmMagazine magComp = gameObjectCallback.Result.GetComponent<FVRFireArmMagazine>();
                        magazineCache.Magazines.Add(magazine.ItemID);

                        if (magComp != null)
                        {
                            magazineCache.AddMagazineData(magComp);
                        }
                    }
                }



                //Loop through all clips and build a list of stripper clip components
                PatchLogger.Log("Loading all clips", PatchLogger.LogType.General);
                foreach(FVRObject clip in clips)
                {
                    if ((DateTime.Now - start).TotalSeconds > 2)
                    {
                        start = DateTime.Now;
                        PatchLogger.Log("-- " + ((int)(((float)progress) / totalObjects * 100)) + "% --", PatchLogger.LogType.General);
                    }
                    PatcherStatus.UpdateProgress(Mathf.Min((float)progress / totalObjects, 0.95f));
                    progress += 1;

                    LastTouchedItem = clip.ItemID;

                    //If this clip isn't cached, then we should store it's data
                    if (!magazineCache.Clips.Contains(clip.ItemID))
                    {
                        gameObjectCallback = clip.GetGameObjectAsync();
                        yield return AnvilManager.Instance.RunDriven(gameObjectCallback);
                        if (gameObjectCallback.Result == null) PatchLogger.LogError("No object was found to use FVRObject! ItemID: " + clip.ItemID);

                        FVRFireArmClip clipComp = gameObjectCallback.Result.GetComponent<FVRFireArmClip>();
                        magazineCache.Clips.Add(clip.ItemID);

                        if (clipComp != null)
                        {
                            magazineCache.AddClipData(clipComp);
                        }
                    }
                }



                //Loop through all bullets and build a list of bullet components
                PatchLogger.Log("Loading all bullets", PatchLogger.LogType.General);
                foreach (FVRObject bullet in bullets)
                {
                    if ((DateTime.Now - start).TotalSeconds > 2)
                    {
                        start = DateTime.Now;
                        PatchLogger.Log("-- " + ((int)(((float)progress) / totalObjects * 100)) + "% --", PatchLogger.LogType.General);
                    }
                    PatcherStatus.UpdateProgress(Mathf.Min((float)progress / totalObjects, 0.95f));
                    progress += 1;

                    LastTouchedItem = bullet.ItemID;

                    //If this bullet isn't cached, then we should store it's data
                    if (!magazineCache.Bullets.Contains(bullet.ItemID))
                    {
                        gameObjectCallback = bullet.GetGameObjectAsync();
                        yield return AnvilManager.Instance.RunDriven(gameObjectCallback);
                        if (gameObjectCallback.Result == null) PatchLogger.LogError("No object was found to use FVRObject! ItemID: " + bullet.ItemID);

                        FVRFireArmRound bulletComp = gameObjectCallback.Result.GetComponent<FVRFireArmRound>();
                        magazineCache.Bullets.Add(bullet.ItemID);

                        if (bulletComp != null)
                        {
                            magazineCache.AddBulletData(bulletComp);
                        }
                    }
                }



                //Load all firearms into the cache
                PatchLogger.Log("Loading all firearms", PatchLogger.LogType.General);
                foreach(FVRObject firearm in firearms)
                {
                    if ((DateTime.Now - start).TotalSeconds > 2)
                    {
                        start = DateTime.Now;
                        PatchLogger.Log("-- " + ((int)(((float)progress) / totalObjects * 100)) + "% --", PatchLogger.LogType.General);
                    }
                    PatcherStatus.UpdateProgress(Mathf.Min((float)progress / totalObjects, 0.95f));
                    progress += 1;

                    LastTouchedItem = firearm.ItemID;

                    //If this firearm isn't cached, then we should store it's data
                    if (!magazineCache.Firearms.Contains(firearm.ItemID))
                    {
                        gameObjectCallback = firearm.GetGameObjectAsync();
                        yield return AnvilManager.Instance.RunDriven(gameObjectCallback);
                        if (gameObjectCallback.Result == null) PatchLogger.LogError("No object was found to use FVRObject! ItemID: " + firearm.ItemID);

                        FVRFireArm firearmComp = gameObjectCallback.Result.GetComponent<FVRFireArm>();
                        magazineCache.Firearms.Add(firearm.ItemID);

                        //If this firearm is valid, then we create a magazine cache entry for it
                        if(firearmComp != null)
                        {
                            MagazineCacheEntry entry = new MagazineCacheEntry();
                            entry.FirearmID = firearm.ItemID;
                            entry.MagType = firearmComp.MagazineType;
                            entry.ClipType = firearmComp.ClipType;
                            entry.BulletType = firearmComp.RoundType;
                            magazineCache.Entries.Add(firearm.ItemID, entry);
                        }
                    }
                }


                //Now that all relevant data is saved, we should go back through all entries and add compatible ammo objects
                foreach (MagazineCacheEntry entry in magazineCache.Entries.Values)
                {
                    if (magazineCache.MagazineData.ContainsKey(entry.MagType))
                    {
                        foreach (AmmoObjectDataTemplate magazine in magazineCache.MagazineData[entry.MagType])
                        {
                            if (!entry.CompatibleMagazines.Contains(magazine.ObjectID))
                            {
                                entry.CompatibleMagazines.Add(magazine.ObjectID);
                            }
                        }
                    }

                    if (magazineCache.ClipData.ContainsKey(entry.ClipType))
                    {
                        foreach (AmmoObjectDataTemplate clip in magazineCache.ClipData[entry.ClipType])
                        {
                            if (!entry.CompatibleClips.Contains(clip.ObjectID))
                            {
                                entry.CompatibleClips.Add(clip.ObjectID);
                            }
                        }
                    }

                    if (magazineCache.BulletData.ContainsKey(entry.BulletType))
                    {
                        foreach (AmmoObjectDataTemplate bullet in magazineCache.BulletData[entry.BulletType])
                        {
                            if (!entry.CompatibleBullets.Contains(bullet.ObjectID))
                            {
                                entry.CompatibleBullets.Add(bullet.ObjectID);
                            }
                        }
                    }
                }

                //Create the cache file 
                PatchLogger.Log("Saving Data", PatchLogger.LogType.General);
                using (StreamWriter sw = File.CreateText(cachePath))
                {
                    string cacheString = JsonConvert.SerializeObject(magazineCache, Formatting.Indented, new StringEnumConverter());
                    sw.WriteLine(cacheString);
                    sw.Close();
                }
            }

            PatchLogger.Log("Applying magazine cache to firearms", PatchLogger.LogType.General);
            ApplyMagazineCache(magazineCache, blacklist);

            PatcherStatus.UpdateProgress(1);
        }



        /// <summary>
        /// Applies the loaded magazine cache onto all firearms, magazines, clips, etc
        /// </summary>
        /// <param name="magazineCache"></param>
        /// <param name="blacklist"></param>
        private static void ApplyMagazineCache(CompatibleMagazineCache magazineCache, Dictionary<string, MagazineBlacklistEntry> blacklist)
        {
            //Apply the magazine cache values to every firearm that is loaded
            foreach (MagazineCacheEntry entry in magazineCache.Entries.Values)
            {
                if (IM.OD.ContainsKey(entry.FirearmID))
                {
                    FVRObject firearm = IM.OD[entry.FirearmID];

                    int MaxCapacityRelated = -1;
                    int MinCapacityRelated = -1;

                    foreach (string mag in entry.CompatibleMagazines)
                    {
                        if (IM.OD.ContainsKey(mag) && (!blacklist.ContainsKey(firearm.ItemID) || !blacklist[firearm.ItemID].MagazineBlacklist.Contains(mag)))
                        {
                            FVRObject magazineObject = IM.OD[mag];

                            firearm.CompatibleMagazines.Add(magazineObject);
                            if (magazineCache.AmmoObjects.ContainsKey(mag)) magazineObject.MagazineCapacity = magazineCache.AmmoObjects[mag].Capacity;

                            if (MaxCapacityRelated < magazineObject.MagazineCapacity) MaxCapacityRelated = magazineObject.MagazineCapacity;
                            if (MinCapacityRelated == -1) MinCapacityRelated = magazineObject.MagazineCapacity;
                            else if (MinCapacityRelated > magazineObject.MagazineCapacity) MinCapacityRelated = magazineObject.MagazineCapacity;
                        }
                    }
                    foreach (string clip in entry.CompatibleClips)
                    {
                        if (IM.OD.ContainsKey(clip) && (!blacklist.ContainsKey(firearm.ItemID) || !blacklist[firearm.ItemID].ClipBlacklist.Contains(clip)))
                        {
                            FVRObject clipObject = IM.OD[clip];

                            firearm.CompatibleClips.Add(clipObject);
                            if (magazineCache.AmmoObjects.ContainsKey(clip)) clipObject.MagazineCapacity = magazineCache.AmmoObjects[clip].Capacity;

                            if (MaxCapacityRelated < clipObject.MagazineCapacity) MaxCapacityRelated = clipObject.MagazineCapacity;
                            if (MinCapacityRelated == -1) MinCapacityRelated = clipObject.MagazineCapacity;
                            else if (MinCapacityRelated > clipObject.MagazineCapacity) MinCapacityRelated = clipObject.MagazineCapacity;
                        }
                    }
                    foreach (string bullet in entry.CompatibleBullets)
                    {
                        if (IM.OD.ContainsKey(bullet) && (!blacklist.ContainsKey(firearm.ItemID) || !blacklist[firearm.ItemID].RoundBlacklist.Contains(bullet)))
                        {
                            firearm.CompatibleSingleRounds.Add(IM.OD[bullet]);
                        }
                    }

                    if (MaxCapacityRelated != -1) firearm.MaxCapacityRelated = MaxCapacityRelated;
                    if (MinCapacityRelated != -1) firearm.MinCapacityRelated = MinCapacityRelated;
                }
            }
        }



        private static bool IsMagazineCacheValid(CompatibleMagazineCache magazineCache)
        {
            bool cacheValid = true;

            //NOTE: you could return false immediately in here, but we don't for the sake of debugging
            foreach (string mag in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Magazine].Select(f => f.ItemID))
            {
                if (!magazineCache.Magazines.Contains(mag))
                {
                    PatchLogger.LogWarning("Magazine not found in cache: " + mag);
                    cacheValid = false;
                }
            }

            foreach (string firearm in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Firearm].Select(f => f.ItemID))
            {
                if (!magazineCache.Firearms.Contains(firearm))
                {
                    PatchLogger.LogWarning("Firearm not found in cache: " + firearm);
                    cacheValid = false;
                }
            }

            foreach (string clip in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Clip].Select(f => f.ItemID))
            {
                if (!magazineCache.Clips.Contains(clip))
                {
                    PatchLogger.LogWarning("Clip not found in cache: " + clip);
                    cacheValid = false;
                }
            }

            foreach (string bullet in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Cartridge].Select(f => f.ItemID))
            {
                if (!magazineCache.Bullets.Contains(bullet))
                {
                    PatchLogger.LogWarning("Bullet not found in cache: " + bullet);
                    cacheValid = false;
                }
            }

            return cacheValid;
        }

    }
}
