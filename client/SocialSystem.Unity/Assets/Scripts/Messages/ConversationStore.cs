using System;
using System.Collections.Generic;
using UnityEngine;

namespace SocialSystem.Client.Messages
{
    [Serializable]
    public sealed class ConversationContact
    {
        public long id;
        public string username;
        public string nickname;
        public bool removed;
    }
    // Local navigation metadata only. Message content and credentials never enter this store.
    public sealed class ConversationStore
    {
        [Serializable] private sealed class SavedContacts { public List<ConversationContact> items = new List<ConversationContact>(); }
        private readonly string key;
        private readonly long ownerId;
        private SavedContacts saved = new SavedContacts();
        public IReadOnlyList<ConversationContact> Items => saved.items;
        private static string Key(string server, long owner) => "SocialSystem.RecentContacts." + server.TrimEnd('/') + "." + owner;
        public ConversationStore(string server, long owner)
        {
            ownerId = owner;
            key = Key(server, owner);
            try
            {
                if (PlayerPrefs.HasKey(key)) saved = JsonUtility.FromJson<SavedContacts>(PlayerPrefs.GetString(key));
                if (saved?.items == null) saved = new SavedContacts();
                var seen = new HashSet<long>();
                saved.items.RemoveAll(x => x == null || x.id <= 0 || x.id == owner || !seen.Add(x.id));
            }
            catch (Exception) { saved = new SavedContacts(); }
        }
        public ConversationContact Remember(long id, string username, string nickname, bool removed)
        {
            if (id <= 0 || id == ownerId) throw new ArgumentException("Invalid conversation contact.");
            var contact = saved.items.Find(x => x.id == id) ?? new ConversationContact { id = id };
            saved.items.Remove(contact);
            contact.username = username ?? "";
            contact.nickname = nickname ?? "";
            contact.removed = removed;
            saved.items.Insert(0, contact);
            try
            {
                PlayerPrefs.SetString(key, JsonUtility.ToJson(saved));
                PlayerPrefs.Save();
            }
            catch (Exception) { Debug.LogWarning("Recent contact navigation could not be saved."); }
            return contact;
        }
        public ConversationContact Find(long id) => saved.items.Find(x => x.id == id);
        // Used by isolated tests to remove only their own account/server metadata.
        public static void Clear(string server, long owner)
        {
            PlayerPrefs.DeleteKey(Key(server, owner));
            PlayerPrefs.Save();
        }
    }
}
