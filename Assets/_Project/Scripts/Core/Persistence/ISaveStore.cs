public interface ISaveStore
{
    string Load(string key, string defaultValue = "");
    void   Save(string key, string value);
    void   Delete(string key);
}
