using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace CompanionScan.Mapping
{
    [System.Serializable]
    public class MappedObject
    {
        public string label;
        public Vector3 position;
        public float score;
    }

    public class MappedObjectDatabase : MonoBehaviour
    {
        public List<MappedObject> objects = new List<MappedObject>();

        public void Add(MappedObject mappedObject)
        {
            objects.Add(mappedObject);
        }

        public void SaveToJson(string fileName)
        {
            var path = Path.Combine(Application.persistentDataPath, fileName);
            var json = JsonConvert.SerializeObject(objects, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public void LoadFromJson(string fileName)
        {
            var path = Path.Combine(Application.persistentDataPath, fileName);
            if (!File.Exists(path))
            {
                return;
            }

            var json = File.ReadAllText(path);
            objects = JsonConvert.DeserializeObject<List<MappedObject>>(json) ?? new List<MappedObject>();
        }
    }
}

