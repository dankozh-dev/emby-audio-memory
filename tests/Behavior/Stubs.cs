// Test doubles for behavior tests of the production Plugin.cs. These do not verify Emby ABI or runtime semantics.
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;

namespace MediaBrowser.Common.Configuration { public interface IApplicationPaths { string DataPath { get; } } }
namespace MediaBrowser.Common.Plugins {
 public abstract class BasePlugin<T> where T:new() { public T Configuration { get; }=new(); public virtual Guid Id=>Guid.Empty; public virtual string Name=>""; public virtual string Description=>""; protected BasePlugin(MediaBrowser.Common.Configuration.IApplicationPaths p,MediaBrowser.Model.Serialization.IXmlSerializer x){} }
 public interface IHasWebPages { IEnumerable<MediaBrowser.Model.Plugins.PluginPageInfo> GetPages(); }
}
namespace MediaBrowser.Model.Plugins { public class BasePluginConfiguration{} public class PluginPageInfo { public string Name {get;set;}="";public string EmbeddedResourcePath {get;set;}="";} }
namespace MediaBrowser.Model.Serialization {
 public interface IXmlSerializer{}
 public interface IJsonSerializer { string SerializeToString<T>(T o); T DeserializeFromString<T>(string s); }
}
namespace MediaBrowser.Model.Logging {
 public interface ILogger { void ErrorException(string s,Exception e); void Info(string s,params object?[] o); }
 public interface ILogManager { ILogger GetLogger(string n); }
}
namespace MediaBrowser.Model.Entities {
 public enum MediaStreamType{Audio,Video,Subtitle} public enum UserDataSaveReason{Import}
 public class MediaStream { public int Index{get;set;} public MediaStreamType Type{get;set;} public string Title{get;set;}=""; public string Language{get;set;}=""; }
}
namespace MediaBrowser.Model.Dto {
 public class MediaSourceInfo { public string Id{get;set;}="";public List<MediaStream> MediaStreams{get;set;}=new(); }
 public class BaseItemDto { public string Id{get;set;}=""; }
}
namespace MediaBrowser.Model.Session { public enum ProgressEvent{TimeUpdate,AudioTrackChange,Pause,Unpause} }
namespace MediaBrowser.Controller.Entities {
 public class BaseItem{ public long InternalId{get;set;} public Guid Id{get;set;}=Guid.NewGuid(); }
 public class User{public Guid Id{get;set;}=Guid.NewGuid();}
 public class UserItemData {
  public int? AudioStreamIndex{get;set;} public int? SubtitleStreamIndex{get;set;} public bool Played{get;set;}
  public int PlayCount{get;set;} public long PlaybackPositionTicks{get;set;} public string Key{get;set;}="";
  public DateTime? LastPlayedDate{get;set;} public bool IsFavorite{get;set;} public bool HideFromResume{get;set;}
  public double? Rating{get;set;} public DateTime? RatingLastModified{get;set;} public DateTime? PlaystateLastModified{get;set;}
  public DateTime? DateLastSearched{get;set;}
 }
 public class InternalItemsQuery {public BaseItem? Parent{get;set;}public bool Recursive{get;set;}public string[] IncludeItemTypes{get;set;}=Array.Empty<string>();}
}
namespace MediaBrowser.Controller.Entities.TV {
 public class Season:BaseItem{}
 public class Episode:BaseItem {
  public Season? Season{get;set;} public object? CollectionFolders=>null;public object? LibraryOptions=>null;
  public List<MediaSourceInfo> Sources{get;set;}=new();
  public List<MediaSourceInfo> GetMediaSources(User u,bool a,bool b,bool c,bool d,object? e,object? f,CancellationToken ct)=>Sources;
 }
}
namespace MediaBrowser.Controller.Session {
 public class PlayState {public int? AudioStreamIndex{get;set;} }
 public class PlaySession {public PlayState PlayState{get;set;}=new();}
 public class SessionInfo{public string UserId{get;set;}="";public long UserInternalId{get;set;}public BaseItemDto? NowPlayingItem{get;set;} }
 public interface ISessionManager {event EventHandler<PlaybackProgressEventArgs> PlaybackProgress;IEnumerable<SessionInfo> Sessions{get;} }
}
namespace MediaBrowser.Controller.Library {
 public class PlaybackProgressEventArgs:EventArgs {public BaseItem? Item{get;set;} public SessionInfo? Session{get;set;}public PlaySession? PlaySession{get;set;}public string? MediaSourceId{get;set;}public ProgressEvent EventName{get;set;}public bool IsAutomated{get;set;} }
 public class ItemChangeEventArgs:EventArgs {public BaseItem Item{get;set;}=null!;}
 public interface ILibraryManager {event EventHandler<ItemChangeEventArgs> ItemAdded;event EventHandler<ItemChangeEventArgs> ItemUpdated; BaseItem? GetItemById(long id);IEnumerable<BaseItem> GetItemList(InternalItemsQuery q);}
 public interface IUserManager {User? GetUserById(Guid id);}
 public interface IUserDataManager {UserItemData GetUserData(User user,BaseItem item);void SaveUserData(User user,BaseItem item,UserItemData data,UserDataSaveReason reason,CancellationToken ct);}
}
