using Deli;
using Deli.Newtonsoft.Json;
using Deli.Newtonsoft.Json.Converters;
using Deli.Runtime;
using Deli.Setup;
using Deli.VFS;
using Deli.VFS.Disk;
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
        private static string CachePath;
        private static string BlacklistPath;
        private static string LastTouchedItem;

        private void Awake()
        {
            PatchLogger.Init();

            Stages.Setup += OnSetup;
            Stages.Runtime += OnRuntime;
        }

        private void OnSetup(SetupStage setup)
        {
            setup.SetupAssetLoaders[Source, "cache"] = LoadCachePath;
            setup.SetupAssetLoaders[Source, "blacklist"] = LoadBlacklistPath;
        }

        private void LoadCachePath(SetupStage stage, Mod mod, IHandle handle)
        {
            if(handle is not IDiskHandle file)
            {
                throw new ArgumentException("Could not load cache file! Somehow the mod is not a directory!");
            }

            CachePath = file.PathOnDisk;
        }

        private void LoadBlacklistPath(SetupStage stage, Mod mod, IHandle handle)
        {
            if (handle is not IDiskHandle file)
            {
                throw new ArgumentException("Could not load cache file! Somehow the mod is not a directory!");
            }

            BlacklistPath = file.PathOnDisk;
        }

        private void OnRuntime(RuntimeStage runtime)
        {
            PatchLogger.Log("MagazinePatcher runtime has started!", PatchLogger.LogType.General);
            AnvilManager.Instance.StartCoroutine(RunAndCatch(LoadMagazineCacheAsync(), e => 
            {
                PatcherStatus.AppendCacheLog("Something bad happened while caching item: " + LastTouchedItem);
                PatcherStatus.CachingFailed = true;
                PatchLogger.LogError("Something bad happened while caching item: " + LastTouchedItem);
                PatchLogger.LogError(e.ToString());
            }));
        }


        private static Dictionary<string, MagazineBlacklistEntry> GetMagazineCacheBlacklist()
        {
            try
            {
                //If the magazine blacklist file does not exist, log an errpr
                if (string.IsNullOrEmpty(BlacklistPath))
                {
                    throw new FileNotFoundException("The blacklist path is empty!");
                }

                //If the blacklist file is empty, create a new one
                string blacklistString = File.ReadAllText(BlacklistPath);
                if (string.IsNullOrEmpty(blacklistString))
                {
                    return CreateNewBlacklist();
                }

                //If the file does exist, we'll try to deserialize it
                else
                {
                    Dictionary<string, MagazineBlacklistEntry> blacklist = new Dictionary<string, MagazineBlacklistEntry>();
                    
                    List<MagazineBlacklistEntry> blacklistDeserialized = JsonConvert.DeserializeObject<List<MagazineBlacklistEntry>>(blacklistString);

                    foreach (MagazineBlacklistEntry entry in blacklistDeserialized)
                    {
                        blacklist.Add(entry.FirearmID, entry);
                    }

                    return blacklist;
                }
            }

            catch (Exception ex)
            {
                PatchLogger.LogError("Failed to load magazine blacklist! Creating new one! Stack trace below:");
                PatchLogger.LogError(ex.ToString());
                return CreateNewBlacklist();
            }
        }


        private static Dictionary<string, MagazineBlacklistEntry> CreateNewBlacklist()
        {
            PatchLogger.Log("Blacklist does not exist! Building new one", PatchLogger.LogType.General);
            Dictionary<string, MagazineBlacklistEntry> blacklist = new Dictionary<string, MagazineBlacklistEntry>();

            StreamWriter sw = File.CreateText(BlacklistPath);
            List<MagazineBlacklistEntry> blacklistEntry = new List<MagazineBlacklistEntry>();

            MagazineBlacklistEntry sample = new MagazineBlacklistEntry();
            sample.FirearmID = "SKSClassic";
            sample.MagazineWhitelist.Add("None");

            blacklistEntry.Add(sample);

            string blacklistString = JsonConvert.SerializeObject(blacklistEntry, Formatting.Indented);
            sw.WriteLine(blacklistString);
            sw.Close();

            foreach (MagazineBlacklistEntry entry in blacklistEntry)
            {
                blacklist.Add(entry.FirearmID, entry);
            }

            return blacklist;
        }


        private static void PokeOtherLoader()
        {
            OtherLoader.LoaderStatus.GetLoaderProgress();
        }

        private static float GetOtherLoaderProgress()
        {
            return OtherLoader.LoaderStatus.GetLoaderProgress();
        }


        private static IEnumerator LoadMagazineCacheAsync()
        {
            
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
                    canCache = GetOtherLoaderProgress() >= 1;
                }

            } while (!canCache && isOtherloaderLoaded);

            PatcherStatus.AppendCacheLog("Caching Started");
            
            bool isCacheValid = false;

            //If the cache exists, we load it and check it's validity
            if (!string.IsNullOrEmpty(CachePath))
            {
                try
                {
                    string cacheJson = File.ReadAllText(CachePath);
                    CompatibleMagazineCache cache = JsonConvert.DeserializeObject<CompatibleMagazineCache>(cacheJson);
                    CompatibleMagazineCache.Instance = cache;

                    isCacheValid = IsMagazineCacheValid(CompatibleMagazineCache.Instance);

                    PatchLogger.Log("Cache file found! Is Valid? " + isCacheValid, PatchLogger.LogType.General);
                }
                catch(Exception e)
                {
                    CompatibleMagazineCache cache = new CompatibleMagazineCache();
                    CompatibleMagazineCache.Instance = cache;

                    PatchLogger.LogError("Failed to read cache file!");
                    PatchLogger.LogError(e.ToString());
                }
            }

            else
            {
                PatchLogger.Log("Cache file not found!", PatchLogger.LogType.General);
                CachePath = Path.Combine(BepInEx.Paths.PluginPath, "CachedCompatibleMags.json");
                CompatibleMagazineCache cache = new CompatibleMagazineCache();
                CompatibleMagazineCache.Instance = cache;
            }

            CompatibleMagazineCache.BlacklistEntries = GetMagazineCacheBlacklist();

            //If the magazine cache file didn't exist, or wasn't valid, we must build a new one
            if (!isCacheValid)
            {
                PatchLogger.Log("Building new magazine cache -- This may take a while!", PatchLogger.LogType.General);

                //Create lists of each category of item
                List<FVRObject> magazines = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Magazine];
                List<FVRObject> clips = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Clip];
                List<FVRObject> speedloaders = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.SpeedLoader];
                List<FVRObject> bullets = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Cartridge];
                List<FVRObject> firearms = IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Firearm];
                AnvilCallback<GameObject> gameObjectCallback;
                int totalObjects = magazines.Count + clips.Count + bullets.Count + speedloaders.Count + firearms.Count;
                int progress = 0;
                DateTime start = DateTime.Now;


                //Loop through all magazines and build a list of magazine components
                PatchLogger.Log("Loading all magazines", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Caching Magazines");
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
                    if (!CompatibleMagazineCache.Instance.Magazines.Contains(magazine.ItemID))
                    {
                        CompatibleMagazineCache.Instance.Magazines.Add(magazine.ItemID);

                        gameObjectCallback = magazine.GetGameObjectAsync();
                        yield return AnvilManager.Instance.RunDriven(gameObjectCallback);
                        if (gameObjectCallback.Result == null)
                        {
                            PatchLogger.LogWarning("No object was found to use FVRObject! ItemID: " + magazine.ItemID);
                            continue;
                        } 

                        FVRFireArmMagazine magComp = gameObjectCallback.Result.GetComponent<FVRFireArmMagazine>();

                        if (magComp != null)
                        {
                            CompatibleMagazineCache.Instance.AddMagazineData(magComp);
                        }
                    }
                }



                //Loop through all clips and build a list of stripper clip components
                PatchLogger.Log("Loading all clips", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Caching Clips");
                foreach (FVRObject clip in clips)
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
                    if (!CompatibleMagazineCache.Instance.Clips.Contains(clip.ItemID))
                    {
                        CompatibleMagazineCache.Instance.Clips.Add(clip.ItemID);

                        gameObjectCallback = clip.GetGameObjectAsync();
                        yield return AnvilManager.Instance.RunDriven(gameObjectCallback);
                        if (gameObjectCallback.Result == null)
                        {
                            PatchLogger.LogWarning("No object was found to use FVRObject! ItemID: " + clip.ItemID);
                            continue;
                        }
                        

                        FVRFireArmClip clipComp = gameObjectCallback.Result.GetComponent<FVRFireArmClip>();

                        if (clipComp != null)
                        {
                            CompatibleMagazineCache.Instance.AddClipData(clipComp);
                        }
                    }
                }


                //Loop through all clips and build a list of stripper clip components
                PatchLogger.Log("Loading all speedloaders", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Caching Speedloaders");
                foreach (FVRObject speedloader in speedloaders)
                {
                    if ((DateTime.Now - start).TotalSeconds > 2)
                    {
                        start = DateTime.Now;
                        PatchLogger.Log("-- " + ((int)(((float)progress) / totalObjects * 100)) + "% --", PatchLogger.LogType.General);
                    }
                    PatcherStatus.UpdateProgress(Mathf.Min((float)progress / totalObjects, 0.95f));
                    progress += 1;

                    LastTouchedItem = speedloader.ItemID;

                    //If this clip isn't cached, then we should store it's data
                    if (!CompatibleMagazineCache.Instance.SpeedLoaders.Contains(speedloader.ItemID))
                    {
                        CompatibleMagazineCache.Instance.SpeedLoaders.Add(speedloader.ItemID);

                        gameObjectCallback = speedloader.GetGameObjectAsync();
                        yield return AnvilManager.Instance.RunDriven(gameObjectCallback);
                        if (gameObjectCallback.Result == null)
                        {
                            PatchLogger.LogWarning("No object was found to use FVRObject! ItemID: " + speedloader.ItemID);
                            continue;
                        }

                        Speedloader speedloaderComp = gameObjectCallback.Result.GetComponent<Speedloader>();
                        if (speedloaderComp != null)
                        {
                            CompatibleMagazineCache.Instance.AddSpeedLoaderData(speedloaderComp);
                        }
                    }
                }



                //Loop through all bullets and build a list of bullet components
                PatchLogger.Log("Loading all bullets", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Caching Bullets");
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
                    if (!CompatibleMagazineCache.Instance.Bullets.Contains(bullet.ItemID))
                    {
                        CompatibleMagazineCache.Instance.Bullets.Add(bullet.ItemID);

                        gameObjectCallback = bullet.GetGameObjectAsync();
                        yield return AnvilManager.Instance.RunDriven(gameObjectCallback);
                        if (gameObjectCallback.Result == null)
                        {
                            PatchLogger.LogWarning("No object was found to use FVRObject! ItemID: " + bullet.ItemID);
                            continue;
                        }
                        

                        FVRFireArmRound bulletComp = gameObjectCallback.Result.GetComponent<FVRFireArmRound>();

                        if (bulletComp != null)
                        {
                            CompatibleMagazineCache.Instance.AddBulletData(bulletComp);
                        }
                    }
                }



                //Load all firearms into the cache
                PatchLogger.Log("Loading all firearms", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Caching Firearms");
                foreach (FVRObject firearm in firearms)
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
                    if (!CompatibleMagazineCache.Instance.Firearms.Contains(firearm.ItemID))
                    {
                        CompatibleMagazineCache.Instance.Firearms.Add(firearm.ItemID);

                        gameObjectCallback = firearm.GetGameObjectAsync();
                        yield return AnvilManager.Instance.RunDriven(gameObjectCallback);
                        if (gameObjectCallback.Result == null)
                        {
                            PatchLogger.LogWarning("No object was found to use FVRObject! ItemID: " + firearm.ItemID);
                            continue;
                        }

                        if (!IM.OD.ContainsKey(firearm.ItemID))
                        {
                            PatchLogger.LogWarning("Item not found in Object Dictionary! It will not be patched! ItemID: " + firearm.ItemID);
                            continue;
                        }


                        //If this firearm is valid, then we create a magazine cache entry for it
                        FVRFireArm firearmComp = gameObjectCallback.Result.GetComponent<FVRFireArm>();
                        if (firearmComp != null)
                        {
                            MagazineCacheEntry entry = new MagazineCacheEntry();
                            entry.FirearmID = firearm.ItemID;
                            entry.MagType = firearmComp.MagazineType;
                            entry.ClipType = firearmComp.ClipType;
                            entry.BulletType = firearmComp.RoundType;

                            //Extra part that handles revolver capacity for speedloader compatibility
                            Revolver revolverComp = firearmComp.gameObject.GetComponent<Revolver>();
                            if(revolverComp != null)
                            {
                                entry.DoesUseSpeedloader = true;
                                IM.OD[entry.FirearmID].MagazineCapacity = revolverComp.Chambers.Length;
                            }

                            CompatibleMagazineCache.Instance.Entries.Add(firearm.ItemID, entry);
                        }
                    }
                }


                //Now that all relevant data is saved, we should go back through all entries and add compatible ammo objects
                PatchLogger.Log("Building Cache Entries", PatchLogger.LogType.General);
                PatcherStatus.AppendCacheLog("Applying Changes");
                foreach (MagazineCacheEntry entry in CompatibleMagazineCache.Instance.Entries.Values)
                {
                    if (!IM.OD.ContainsKey(entry.FirearmID)) continue;
                    LastTouchedItem = entry.FirearmID;

                    if (CompatibleMagazineCache.Instance.MagazineData.ContainsKey(entry.MagType))
                    {
                        foreach (AmmoObjectDataTemplate magazine in CompatibleMagazineCache.Instance.MagazineData[entry.MagType])
                        {
                            if (!entry.CompatibleMagazines.Contains(magazine.ObjectID))
                            {
                                entry.CompatibleMagazines.Add(magazine.ObjectID);
                            }
                        }
                    }

                    if (CompatibleMagazineCache.Instance.ClipData.ContainsKey(entry.ClipType))
                    {
                        foreach (AmmoObjectDataTemplate clip in CompatibleMagazineCache.Instance.ClipData[entry.ClipType])
                        {
                            if (!entry.CompatibleClips.Contains(clip.ObjectID))
                            {
                                entry.CompatibleClips.Add(clip.ObjectID);
                            }
                        }
                    }

                    if (entry.DoesUseSpeedloader && CompatibleMagazineCache.Instance.SpeedLoaderData.ContainsKey(entry.BulletType))
                    {
                        foreach (AmmoObjectDataTemplate speedloader in CompatibleMagazineCache.Instance.SpeedLoaderData[entry.BulletType])
                        {
                            if (!entry.CompatibleSpeedLoaders.Contains(speedloader.ObjectID) && IM.OD[entry.FirearmID].MagazineCapacity == speedloader.Capacity)
                            {
                                entry.CompatibleSpeedLoaders.Add(speedloader.ObjectID);
                            }
                        }
                    }
                    
                    if (CompatibleMagazineCache.Instance.BulletData.ContainsKey(entry.BulletType))
                    {
                        foreach (AmmoObjectDataTemplate bullet in CompatibleMagazineCache.Instance.BulletData[entry.BulletType])
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
                PatcherStatus.AppendCacheLog("Saving");
                using (StreamWriter sw = File.CreateText(CachePath))
                {
                    string cacheString = JsonConvert.SerializeObject(CompatibleMagazineCache.Instance, Formatting.Indented);
                    sw.WriteLine(cacheString);
                    sw.Close();
                }
            }

            PatchLogger.Log("Applying magazine cache to firearms", PatchLogger.LogType.General);
            ApplyMagazineCache(CompatibleMagazineCache.Instance);
            RemoveBlacklistedMagazines(CompatibleMagazineCache.BlacklistEntries);

            PatcherStatus.UpdateProgress(1);
        }



        /// <summary>
        /// Applies the loaded magazine cache onto all firearms, magazines, clips, etc
        /// </summary>
        /// <param name="magazineCache"></param>
        /// <param name="blacklist"></param>
        private static void ApplyMagazineCache(CompatibleMagazineCache magazineCache)
        {
            //Apply the magazine cache values to every firearm that is loaded
            foreach (MagazineCacheEntry entry in magazineCache.Entries.Values)
            {
                if (IM.OD.ContainsKey(entry.FirearmID))
                {
                    FVRObject firearm = IM.OD[entry.FirearmID];
                    LastTouchedItem = entry.FirearmID;

                    int MaxCapacityRelated = -1;
                    int MinCapacityRelated = -1;

                    foreach (string mag in entry.CompatibleMagazines)
                    {
                        if (IM.OD.ContainsKey(mag) && (!firearm.CompatibleMagazines.Any(o => (o != null && o.ItemID == mag))))
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
                        if (IM.OD.ContainsKey(clip) && (!firearm.CompatibleClips.Any(o => (o != null && o.ItemID == clip))))
                        {
                            FVRObject clipObject = IM.OD[clip];

                            firearm.CompatibleClips.Add(clipObject);
                            if (magazineCache.AmmoObjects.ContainsKey(clip)) clipObject.MagazineCapacity = magazineCache.AmmoObjects[clip].Capacity;

                            if (MaxCapacityRelated < clipObject.MagazineCapacity) MaxCapacityRelated = clipObject.MagazineCapacity;
                            if (MinCapacityRelated == -1) MinCapacityRelated = clipObject.MagazineCapacity;
                            else if (MinCapacityRelated > clipObject.MagazineCapacity) MinCapacityRelated = clipObject.MagazineCapacity;
                        }
                    }
                    foreach (string speedloader in entry.CompatibleSpeedLoaders)
                    {
                        if (IM.OD.ContainsKey(speedloader) && (!firearm.CompatibleSpeedLoaders.Any(o => (o != null && o.ItemID == speedloader))))
                        {
                            FVRObject speedloaderObject = IM.OD[speedloader];

                            firearm.CompatibleSpeedLoaders.Add(speedloaderObject);
                            if (magazineCache.AmmoObjects.ContainsKey(speedloader)) speedloaderObject.MagazineCapacity = magazineCache.AmmoObjects[speedloader].Capacity;

                            if (MaxCapacityRelated < speedloaderObject.MagazineCapacity) MaxCapacityRelated = speedloaderObject.MagazineCapacity;
                            if (MinCapacityRelated == -1) MinCapacityRelated = speedloaderObject.MagazineCapacity;
                            else if (MinCapacityRelated > speedloaderObject.MagazineCapacity) MinCapacityRelated = speedloaderObject.MagazineCapacity;
                        }
                    }
                    foreach (string bullet in entry.CompatibleBullets)
                    {
                        if (IM.OD.ContainsKey(bullet) && (!firearm.CompatibleSingleRounds.Any(o => (o != null && o.ItemID == bullet))))
                        {
                            firearm.CompatibleSingleRounds.Add(IM.OD[bullet]);
                        }
                    }

                    if (MaxCapacityRelated != -1) firearm.MaxCapacityRelated = MaxCapacityRelated;
                    if (MinCapacityRelated != -1) firearm.MinCapacityRelated = MinCapacityRelated;
                }
            }

            //This part specifically fills out the IM.CompatMags dictionary for every magazine
            foreach(KeyValuePair<FireArmMagazineType, List<AmmoObjectDataTemplate>> pair in CompatibleMagazineCache.Instance.MagazineData)
            {
                if(!IM.CompatMags.ContainsKey(pair.Key))
                {
                    IM.CompatMags.Add(pair.Key, new List<FVRObject>());
                }

                List<FVRObject> loadedMags = new List<FVRObject>();
                foreach(AmmoObjectDataTemplate magTemplate in pair.Value)
                {
                    if (IM.OD.ContainsKey(magTemplate.ObjectID))
                    {
                        FVRObject mag = IM.OD[magTemplate.ObjectID];
                        mag.MagazineType = pair.Key;
                        loadedMags.Add(mag);
                    }
                }
                IM.CompatMags[pair.Key] = loadedMags;
            }

        }


        private static void RemoveBlacklistedMagazines(Dictionary<string, MagazineBlacklistEntry> blacklist)
        {
            foreach(FVRObject firearm in IM.Instance.odicTagCategory[FVRObject.ObjectCategory.Firearm])
            {
                if (blacklist.ContainsKey(firearm.ItemID))
                {
                    for(int i = firearm.CompatibleMagazines.Count - 1; i >= 0; i--)
                    {
                        if (!blacklist[firearm.ItemID].IsMagazineAllowed(firearm.CompatibleMagazines[i].ItemID))
                        {
                            firearm.CompatibleMagazines.RemoveAt(i);
                        }
                    }

                    for (int i = firearm.CompatibleClips.Count - 1; i >= 0; i--)
                    {
                        if (!blacklist[firearm.ItemID].IsClipAllowed(firearm.CompatibleClips[i].ItemID))
                        {
                            firearm.CompatibleClips.RemoveAt(i);
                        }
                    }

                    for (int i = firearm.CompatibleSingleRounds.Count - 1; i >= 0; i--)
                    {
                        if (!blacklist[firearm.ItemID].IsRoundAllowed(firearm.CompatibleSingleRounds[i].ItemID))
                        {
                            firearm.CompatibleSingleRounds.RemoveAt(i);
                        }
                    }

                    for (int i = firearm.CompatibleSpeedLoaders.Count - 1; i >= 0; i--)
                    {
                        if (!blacklist[firearm.ItemID].IsSpeedloaderAllowed(firearm.CompatibleSpeedLoaders[i].ItemID))
                        {
                            firearm.CompatibleSpeedLoaders.RemoveAt(i);
                        }
                    }
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


        public static IEnumerator RunAndCatch(IEnumerator routine, Action<Exception> onError = null)
        {
            bool more = true;
            while (more)
            {
                try
                {
                    more = routine.MoveNext();
                }
                catch (Exception e)
                {
                    if (onError != null)
                    {
                        onError(e);
                    }

                    yield break;
                }

                if (more)
                {
                    yield return routine.Current;
                }
            }
        }

    }
}
