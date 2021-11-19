using Deli.Newtonsoft.Json;
using FistVR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MagazinePatcher
{
    public class CompatibleMagazineCache
    {
        public List<string> Firearms;
        public List<string> Magazines;
        public List<string> Clips;
        public List<string> SpeedLoaders;
        public List<string> Bullets;
        

        public Dictionary<string, MagazineCacheEntry> Entries;
        public Dictionary<string, AmmoObjectDataTemplate> AmmoObjects;

        public Dictionary<FireArmMagazineType, List<AmmoObjectDataTemplate>> MagazineData;
        public Dictionary<FireArmClipType, List<AmmoObjectDataTemplate>> ClipData;
        public Dictionary<FireArmRoundType, List<AmmoObjectDataTemplate>> SpeedLoaderData;
        public Dictionary<FireArmRoundType, List<AmmoObjectDataTemplate>> BulletData;
        
        public static CompatibleMagazineCache Instance;

        public static Dictionary<string, MagazineBlacklistEntry> BlacklistEntries;


        public CompatibleMagazineCache()
        {
            Firearms = new List<string>();
            Magazines = new List<string>();
            Clips = new List<string>();
            SpeedLoaders = new List<string>();
            Bullets = new List<string>();

            Entries = new Dictionary<string, MagazineCacheEntry>();
            AmmoObjects = new Dictionary<string, AmmoObjectDataTemplate>();

            MagazineData = new Dictionary<FireArmMagazineType, List<AmmoObjectDataTemplate>>();
            ClipData = new Dictionary<FireArmClipType, List<AmmoObjectDataTemplate>>();
            SpeedLoaderData = new Dictionary<FireArmRoundType, List<AmmoObjectDataTemplate>>();
            BulletData = new Dictionary<FireArmRoundType, List<AmmoObjectDataTemplate>>();

            BlacklistEntries = new Dictionary<string, MagazineBlacklistEntry>();
        }


        public void AddMagazineData(FVRFireArmMagazine mag)
        {
            if (!MagazineData.ContainsKey(mag.MagazineType))
            {
                MagazineData.Add(mag.MagazineType, new List<AmmoObjectDataTemplate>());
            }
            MagazineData[mag.MagazineType].Add(new AmmoObjectDataTemplate(mag));

            if (!AmmoObjects.ContainsKey(mag.ObjectWrapper.ItemID))
            {
                AmmoObjects.Add(mag.ObjectWrapper.ItemID, new AmmoObjectDataTemplate(mag));
            }
        }

        public void AddClipData(FVRFireArmClip clip)
        {
            if (!ClipData.ContainsKey(clip.ClipType))
            {
                ClipData.Add(clip.ClipType, new List<AmmoObjectDataTemplate>());
            }
            ClipData[clip.ClipType].Add(new AmmoObjectDataTemplate(clip));

            if (!AmmoObjects.ContainsKey(clip.ObjectWrapper.ItemID))
            {
                AmmoObjects.Add(clip.ObjectWrapper.ItemID, new AmmoObjectDataTemplate(clip));
            }
        }

        public void AddSpeedLoaderData(Speedloader speedloader)
        {
            if (!SpeedLoaderData.ContainsKey(speedloader.Chambers[0].Type))
            {
                SpeedLoaderData.Add(speedloader.Chambers[0].Type, new List<AmmoObjectDataTemplate>());
            }
            SpeedLoaderData[speedloader.Chambers[0].Type].Add(new AmmoObjectDataTemplate(speedloader));

            if (!AmmoObjects.ContainsKey(speedloader.ObjectWrapper.ItemID))
            {
                AmmoObjects.Add(speedloader.ObjectWrapper.ItemID, new AmmoObjectDataTemplate(speedloader));
            }
        }

        public void AddBulletData(FVRFireArmRound bullet)
        {
            if (!BulletData.ContainsKey(bullet.RoundType))
            {
                BulletData.Add(bullet.RoundType, new List<AmmoObjectDataTemplate>());
            }
            BulletData[bullet.RoundType].Add(new AmmoObjectDataTemplate(bullet));

            if (!AmmoObjects.ContainsKey(bullet.ObjectWrapper.ItemID))
            {
                AmmoObjects.Add(bullet.ObjectWrapper.ItemID, new AmmoObjectDataTemplate(bullet));
            }
        }
    }

    public class MagazineCacheEntry
    {
        public string FirearmID;
        public FireArmMagazineType MagType;
        public FireArmClipType ClipType;
        public FireArmRoundType BulletType;
        public bool DoesUseSpeedloader;
        public List<string> CompatibleMagazines;
        public List<string> CompatibleClips;
        public List<string> CompatibleSpeedLoaders;
        public List<string> CompatibleBullets;



        public MagazineCacheEntry()
        {
            CompatibleMagazines = new List<string>();
            CompatibleClips = new List<string>();
            CompatibleSpeedLoaders = new List<string>();
            CompatibleBullets = new List<string>();
        }
    }


    public class AmmoObjectDataTemplate
    {
        public string ObjectID;
        public int Capacity;
        public FireArmMagazineType MagType;
        public FireArmRoundType RoundType;

        public AmmoObjectDataTemplate() { }

        public AmmoObjectDataTemplate(FVRFireArmMagazine mag)
        {
            ObjectID = mag.ObjectWrapper.ItemID;
            Capacity = mag.m_capacity;
            MagType = mag.MagazineType;
            RoundType = mag.RoundType;
        }

        public AmmoObjectDataTemplate(FVRFireArmClip clip)
        {
            ObjectID = clip.ObjectWrapper.ItemID;
            Capacity = clip.m_capacity;
            MagType = FireArmMagazineType.mNone;
            RoundType = clip.RoundType;
        }

        public AmmoObjectDataTemplate(Speedloader speedloader)
        {
            ObjectID = speedloader.ObjectWrapper.ItemID;
            Capacity = speedloader.Chambers.Count;
            MagType = FireArmMagazineType.mNone;
            RoundType = speedloader.Chambers[0].Type;
        }

        public AmmoObjectDataTemplate(FVRFireArmRound bullet)
        {
            ObjectID = bullet.ObjectWrapper.ItemID;
            Capacity = -1;
            MagType = FireArmMagazineType.mNone;
            RoundType = bullet.RoundType;
        }
    }


    public class MagazineBlacklistEntry
    {
        public string FirearmID;
        public List<string> MagazineBlacklist = new List<string>();
        public List<string> MagazineWhitelist = new List<string>();
        public List<string> ClipBlacklist = new List<string>();
        public List<string> ClipWhitelist = new List<string>();
        public List<string> SpeedLoaderBlacklist = new List<string>();
        public List<string> SpeedLoaderWhitelist = new List<string>();
        public List<string> RoundBlacklist = new List<string>();
        public List<string> RoundWhitelist = new List<string>();

        public MagazineBlacklistEntry()
        {
        }

        public bool IsItemBlacklisted(string itemID)
        {
            return MagazineBlacklist.Contains(itemID) || ClipBlacklist.Contains(itemID) || RoundBlacklist.Contains(itemID) || SpeedLoaderBlacklist.Contains(itemID);
        }

        public bool IsMagazineAllowed(string itemID)
        {
            if(MagazineWhitelist.Count > 0 && (!MagazineWhitelist.Contains(itemID))) return false;

            if (MagazineBlacklist.Contains(itemID)) return false;

            return true;
        }

        public bool IsClipAllowed(string itemID)
        {
            if (ClipWhitelist.Count > 0 && (!ClipWhitelist.Contains(itemID))) return false;

            if (ClipBlacklist.Contains(itemID)) return false;

            return true;
        }

        public bool IsSpeedloaderAllowed(string itemID)
        {
            if (SpeedLoaderWhitelist.Count > 0 && (!SpeedLoaderWhitelist.Contains(itemID))) return false;

            if (SpeedLoaderBlacklist.Contains(itemID)) return false;

            return true;
        }

        public bool IsRoundAllowed(string itemID)
        {
            if (RoundWhitelist.Count > 0 && (!RoundWhitelist.Contains(itemID))) return false;

            if (RoundBlacklist.Contains(itemID)) return false;

            return true;
        }
    }
}
