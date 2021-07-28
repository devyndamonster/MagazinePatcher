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
        public List<string> Bullets;

        public Dictionary<string, MagazineCacheEntry> Entries;
        public Dictionary<string, AmmoObjectDataTemplate> AmmoObjects;

        public Dictionary<FireArmMagazineType, List<AmmoObjectDataTemplate>> MagazineData;
        public Dictionary<FireArmClipType, List<AmmoObjectDataTemplate>> ClipData;
        public Dictionary<FireArmRoundType, List<AmmoObjectDataTemplate>> BulletData;

        public static CompatibleMagazineCache Instance;

        public static Dictionary<string, MagazineBlacklistEntry> BlacklistEntries;


        public CompatibleMagazineCache()
        {
            Firearms = new List<string>();
            Magazines = new List<string>();
            Clips = new List<string>();
            Bullets = new List<string>();

            Entries = new Dictionary<string, MagazineCacheEntry>();
            AmmoObjects = new Dictionary<string, AmmoObjectDataTemplate>();

            MagazineData = new Dictionary<FireArmMagazineType, List<AmmoObjectDataTemplate>>();
            ClipData = new Dictionary<FireArmClipType, List<AmmoObjectDataTemplate>>();
            BulletData = new Dictionary<FireArmRoundType, List<AmmoObjectDataTemplate>>();

            BlacklistEntries = new Dictionary<string, MagazineBlacklistEntry>();
        }

        public void PrintBlacklist()
        {
            if(BlacklistEntries.coun)
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
        public List<string> CompatibleMagazines;
        public List<string> CompatibleClips;
        public List<string> CompatibleBullets;

        public MagazineCacheEntry()
        {
            CompatibleMagazines = new List<string>();
            CompatibleClips = new List<string>();
            CompatibleBullets = new List<string>();
        }
    }


    //TODO this is totally unnecessary if we just write a custom serializer for FVRObjects. We should do that eventually!
    public class AmmoObjectDataTemplate
    {
        public string ObjectID;
        public int Capacity;

        [JsonIgnore]
        public FVRObject AmmoObject;

        public AmmoObjectDataTemplate() { }

        public AmmoObjectDataTemplate(FVRFireArmMagazine mag)
        {
            ObjectID = mag.ObjectWrapper.ItemID;
            Capacity = mag.m_capacity;
            AmmoObject = IM.OD[ObjectID];
        }

        public AmmoObjectDataTemplate(FVRFireArmClip clip)
        {
            ObjectID = clip.ObjectWrapper.ItemID;
            Capacity = clip.m_capacity;
            AmmoObject = IM.OD[ObjectID];
        }

        public AmmoObjectDataTemplate(FVRFireArmRound bullet)
        {
            ObjectID = bullet.ObjectWrapper.ItemID;
            Capacity = -1;
            AmmoObject = IM.OD[ObjectID];
        }
    }


    public class MagazineBlacklistEntry
    {
        public string FirearmID;
        public List<string> MagazineBlacklist = new List<string>();
        public List<string> ClipBlacklist = new List<string>();
        public List<string> RoundBlacklist = new List<string>();

        public MagazineBlacklistEntry()
        {
        }

        public bool IsItemBlacklisted(string itemID)
        {
            return MagazineBlacklist.Contains(itemID) || ClipBlacklist.Contains(itemID) || RoundBlacklist.Contains(itemID);
        }
    }
}
