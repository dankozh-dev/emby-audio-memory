using System.Text.Json;
using Emby.AudioMemory;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;

int checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception("FAILED: "+label);checks++;Console.WriteLine("PASS "+label);}
int? Audio(Fixture f,User u,Episode e)=>f.Data.GetUserData(u,e).AudioStreamIndex;
State State(Fixture f)=>JsonSerializer.Deserialize<State>(File.ReadAllText(Path.Combine(f.Dir,"audio-memory/state.json")))!;
string Rest(UserItemData d){var copy=JsonSerializer.Deserialize<UserItemData>(JsonSerializer.Serialize(d))!;copy.AudioStreamIndex=null;return JsonSerializer.Serialize(copy);}

using(var f=new Fixture()) {
 bool denied=false;try{f.P.Seed(f.U2.Id.ToString(),f.E1.InternalId,6,false);}catch(InvalidOperationException){denied=true;}
 Check(denied&&f.Data.Saves==0,"legacy config keeps unselected users excluded");
 f.P.Configuration.EnableForAllUsers=true;
 f.P.Seed(f.U2.Id.ToString(),f.E1.InternalId,6,false);
 Check(Audio(f,f.U2,f.E2)==3&&Audio(f,f.U1,f.E2)==null,"all-users mode learns independently for a user outside the list");
 f.P.Configuration.EnableForAllUsers=false;
 var late=Fixture.Ep(106,f.Season,4);f.Lib.Items.Add(late);f.P.Reconcile(false);
 Check(Audio(f,f.U2,late)==null,"switching back to selected users stops reconciliation for excluded users");
 f.P.Configuration.EnableForAllUsers=true;f.P.Configuration.Enable=false;
 denied=false;try{f.P.Seed(f.U2.Id.ToString(),f.E1.InternalId,6,false);}catch(InvalidOperationException){denied=true;}
 Check(denied,"master switch disables all-users mode");
}
using(var f=new Fixture()) {
 var original=f.Data.GetUserData(f.U1,f.E2); original.Played=true;original.PlayCount=7;original.PlaybackPositionTicks=987654321;original.LastPlayedDate=DateTime.Parse("2025-06-01T11:00:00Z");original.SubtitleStreamIndex=9;original.IsFavorite=true;original.Rating=8;original.HideFromResume=true;original.Key="key";original.RatingLastModified=DateTime.UtcNow;original.PlaystateLastModified=DateTime.UtcNow;original.DateLastSearched=DateTime.UtcNow;
 var rest=Rest(original);
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,true);
 Check(f.Data.Saves==0&&!File.Exists(Path.Combine(f.Dir,"audio-memory/state.json")),"dry run changes neither userdata nor durable state");
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 Check(Audio(f,f.U1,f.E1)==6&&Audio(f,f.U1,f.E2)==3,"track title maps to different per-episode indices");
 Check(Audio(f,f.U2,f.E2)==null&&Audio(f,f.U1,f.OtherSeason)==null,"user and season isolation");
 Check(Audio(f,f.U1,f.Ambiguous)==null&&Audio(f,f.U1,f.MultiSource)==null,"ambiguous tracks and multiple sources skipped");
 Check(Audio(f,f.U1,f.Active)==null,"active playback skipped even for another user");
 Check(Rest(f.Data.GetUserData(f.U1,f.E2))==rest&&original.AudioStreamIndex==null,"full userdata retained and original shared object never mutated");
 f.Data.GetUserData(f.U1,f.E2).AudioStreamIndex=1;
 f.P.Reconcile(false);
 Check(Audio(f,f.U1,f.E2)==1,"reconcile preserves later manual selection");
 f.P.Configuration.Enable=false;
 f.P.Rollback(f.U1.Id.ToString(),f.Season.InternalId,false);
 Check(Audio(f,f.U1,f.E1)==null&&Audio(f,f.U1,f.E2)==1,"rollback works disabled and preserves outside changes");
 Check(State(f).Preferences.Single().Active==false,"rollback deactivates remembered preference");
}
using(var f=new Fixture()) {
 f.Raise(f.E1,6,ProgressEvent.TimeUpdate,false);
 f.Raise(f.E1,6,ProgressEvent.AudioTrackChange,true);
 f.Raise(f.E1,6,ProgressEvent.AudioTrackChange,false,"wrong-source");
 Check(f.Data.Saves==0,"ordinary, automated, and wrong-source events cannot train");
 f.Raise(f.E1,6,ProgressEvent.AudioTrackChange,false);
 Check(Audio(f,f.U1,f.E1)==null&&Audio(f,f.U1,f.E2)==3,"manual audio event trains and excludes current episode");
 f.P.Rollback(f.U1.Id.ToString(),f.Season.InternalId,false);
 Check(Audio(f,f.U1,f.E2)==null,"normal rollback restores original null selection");
}
using(var f=new Fixture()) {
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 f.Data.GetUserData(f.U1,f.E2).AudioStreamIndex=1;
 f.Raise(f.E2,1,ProgressEvent.AudioTrackChange,false);
 f.P.Rollback(f.U1.Id.ToString(),f.Season.InternalId,false);
 Check(Audio(f,f.U1,f.E2)==1,"manual event retires rollback claim for current episode");
}
using(var f=new Fixture()) {
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 f.Data.ThrowBeforeSaveOnce=true;
 try{f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,1,false);}catch(InvalidOperationException){}
 Check(State(f).Backups.Single(b=>b.EpisodeId==f.E1.InternalId).Pending,"pending journal durable before failed save");
 f.P.Dispose();f.Start();
 f.P.Rollback(f.U1.Id.ToString(),f.Season.InternalId,false);
 Check(Audio(f,f.U1,f.E1)==null,"crash recovery rollback accepts previous successful value when pending write never happened");
}
using(var f=new Fixture()) {
 f.Data.ThrowAfterSaveOnce=true;
 try{f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);}catch(InvalidOperationException){}
 Check(State(f).Backups.Single().Pending&&Audio(f,f.U1,f.E1)==6,"post-save crash leaves recoverable pending journal");
 f.P.Dispose();f.Start();
 f.P.Rollback(f.U1.Id.ToString(),f.Season.InternalId,false);
 Check(Audio(f,f.U1,f.E1)==null,"pending applied value rolls back after restart");
}
using(var f=new Fixture()) {
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 f.E2.Sources[0].MediaStreams.Single(t=>t.Index==3).Title="New studio";
 f.P.Rollback(f.U1.Id.ToString(),f.Season.InternalId,false);
 Check(Audio(f,f.U1,f.E2)==3,"changed media layout prevents unsafe rollback");
}
using(var f=new Fixture()) {
 f.Data.OnRead=(u,e,n,d)=>{if(e==f.E2&&n==3)d.AudioStreamIndex=1;};
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 Check(Audio(f,f.U1,f.E2)==1,"concurrent change after journal fsync is preserved even when GetUserData returns shared object");
}
using(var f=new Fixture()) {
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 var late=Fixture.Ep(106,f.Season,4);f.Lib.Items.Add(late);
 f.Data.OnRead=(u,e,n,d)=>{if(e==late&&n==2)d.AudioStreamIndex=1;};
 f.P.Reconcile(false);
 Check(Audio(f,f.U1,late)==1,"reconcile repeats ownership check on fresh second read");
}
using(var f=new Fixture()) {
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 var late=Fixture.Ep(106,f.Season,4);f.Lib.Items.Add(late);f.Lib.RaiseAdded(late);
 Check(Audio(f,f.U1,late)==4,"new episode library event applies existing season preference");
}

// The user accepts different voice/order when a title is unavailable. The fallback is
// the actual MediaStream.Index, never its position within the audio-only list.
foreach(var sourceTitle in new string?[]{"",null," \t\u00a0 "}) using(var f=new Fixture()) {
 var source=f.E1.Sources[0].MediaStreams.Single(t=>t.Index==6);source.Title=sourceTitle!;source.Language="";
 var target=f.E2.Sources[0].MediaStreams.Single(t=>t.Index==3);target.Index=6;target.Title="Different studio";target.Language="eng";
 var original=f.Data.GetUserData(f.U1,f.E2);original.AudioStreamIndex=1;original.Played=true;original.PlayCount=9;original.PlaybackPositionTicks=1234567;original.SubtitleStreamIndex=9;original.IsFavorite=true;
 var originalRest=Rest(original);var label=sourceTitle==null?"null":sourceTitle.Length==0?"empty":"whitespace";
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,true);
 Check(f.Data.Saves==0,"index mode dry run is read-only: "+label);
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 Check(Audio(f,f.U1,f.E1)==6&&Audio(f,f.U1,f.E2)==6,"blank source title and language select actual index despite target title/language: "+label);
 Check(Audio(f,f.U2,f.E2)==null&&Audio(f,f.U1,f.OtherSeason)==null&&Audio(f,f.U1,f.Active)==null&&Audio(f,f.U1,f.MultiSource)==null,"index mode preserves user, season, playback, and media-source boundaries: "+label);
 Check(Rest(f.Data.GetUserData(f.U1,f.E2))==originalRest&&original.AudioStreamIndex==1,"index mode preserves other userdata and original shared instance: "+label);
 var saved=State(f).Preferences.Single();
 Check(string.IsNullOrWhiteSpace(saved.Title)&&saved.SourceAudioStreamIndex==6&&saved.SeasonId==f.Season.InternalId,"index preference persists without invented title: "+label);
 f.P.Dispose();f.Start();var late=Fixture.Ep(106,f.Season,6);late.Sources[0].MediaStreams.Single(t=>t.Index==6).Title="New title";f.Lib.Items.Add(late);f.P.Reconcile(false);
 Check(Audio(f,f.U1,late)==6,"index preference survives restart and applies to later episode: "+label);
 f.P.Rollback(f.U1.Id.ToString(),f.Season.InternalId,false);
 Check(Audio(f,f.U1,f.E2)==1&&Audio(f,f.U1,late)==null&&Rest(f.Data.GetUserData(f.U1,f.E2))==originalRest,"index mode rollback restores original selections and preserves userdata: "+label);
}
using(var f=new Fixture()) {
 var source=f.E1.Sources[0].MediaStreams.Single(t=>t.Index==6);source.Title="";source.Language=null!;
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 Check(Audio(f,f.U1,f.E2)==null,"index mode skips target missing actual index even with same audio-list position");
}
foreach(var nonAudio in new[]{MediaStreamType.Video,MediaStreamType.Subtitle}) using(var f=new Fixture()) {
 var source=f.E1.Sources[0].MediaStreams.Single(t=>t.Index==6);source.Title="";source.Language=null!;
 var target=f.E2.Sources[0].MediaStreams.Single(t=>t.Index==3);target.Index=6;target.Type=nonAudio;target.Title="";
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 Check(Audio(f,f.U1,f.E2)==null,"index mode skips non-audio actual index: "+nonAudio);
}
using(var f=new Fixture()) {
 var source=f.E1.Sources[0].MediaStreams.Single(t=>t.Index==6);source.Title="";source.Language="";
 f.E2.Sources[0].MediaStreams.Single(t=>t.Index==3).Index=6;
 f.Raise(f.E1,6,ProgressEvent.AudioTrackChange,false);
 Check(Audio(f,f.U1,f.E1)==null&&Audio(f,f.U1,f.E2)==6&&string.IsNullOrWhiteSpace(State(f).Preferences.Single().Title),"manual AudioTrackChange learns index mode and excludes current episode");
}
foreach(var targetTitle in new string?[]{"",null," \t "}) using(var f=new Fixture()) {
 var target=f.E2.Sources[0].MediaStreams.Single(t=>t.Index==3);target.Index=6;target.Title=targetTitle!;target.Language="";
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 Check(Audio(f,f.U1,f.E2)==6,"named source falls back to actual index only for blank target title: "+(targetTitle==null?"null":targetTitle.Length==0?"empty":"whitespace"));
}
using(var f=new Fixture()) {
 f.E2.Sources[0].MediaStreams.Add(new(){Index=6,Type=MediaStreamType.Audio,Title="",Language=""});
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 Check(Audio(f,f.U1,f.E2)==3,"unique title match takes priority over blank target at source index");
}
using(var f=new Fixture()) {
 var target=f.E2.Sources[0].MediaStreams.Single(t=>t.Index==3);target.Index=6;target.Title="HDRezka";
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 Check(Audio(f,f.U1,f.E2)==null,"named source never falls back to a conflicting nonblank title");
}
using(var f=new Fixture()) {
 var target=f.E2.Sources[0].MediaStreams.Single(t=>t.Index==3);target.Index=6;target.Language="eng";
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 Check(Audio(f,f.U1,f.E2)==null,"named source never bypasses language mismatch for a named target");
}
using(var f=new Fixture()) {
 f.E2.Sources[0].MediaStreams.Add(new(){Index=4,Type=MediaStreamType.Audio,Title="HDRezka (Dub)",Language="rus"});
 f.E2.Sources[0].MediaStreams.Add(new(){Index=6,Type=MediaStreamType.Audio,Title="",Language=""});
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 Check(Audio(f,f.U1,f.E2)==null,"ambiguous exact title matches cannot be bypassed by blank index fallback");
}
using(var f=new Fixture()) {
 f.E2.Sources[0].MediaStreams.Single(t=>t.Index==3).Title="";
 f.E2.Sources[0].MediaStreams.Add(new(){Index=6,Type=MediaStreamType.Video,Title="",Language=""});
 f.P.Seed(f.U1.Id.ToString(),f.E1.InternalId,6,false);
 Check(Audio(f,f.U1,f.E2)==null,"named source fallback rejects a non-audio target index");
}
using(var f=new Fixture()) {
 // Serialize only the fields written by the original schema-1 release.
 f.P.Dispose();Directory.CreateDirectory(Path.Combine(f.Dir,"audio-memory"));
 File.WriteAllText(Path.Combine(f.Dir,"audio-memory/state.json"),JsonSerializer.Serialize(new {
  SchemaVersion=1,Preferences=new[]{new{UserId=f.U1.Id.ToString("N"),SeasonId=f.Season.InternalId,Title="HDRezka (Dub)",Language="rus",SourceEpisodeId=f.E1.InternalId,SourceAudioStreamIndex=6,Revision=3,UpdatedUtc=DateTime.UtcNow,Active=true}},Backups=Array.Empty<object>()}));
 f.Start();f.P.Reconcile(false);
 Check(Audio(f,f.U1,f.E1)==6&&Audio(f,f.U1,f.E2)==3,"original schema-1 named preference reloads and matches reordered tracks by title");
}
Console.WriteLine($"TOTAL {checks} behavior checks passed");

sealed class Fixture:IDisposable {
 public string Dir=Path.Combine(Path.GetTempPath(),"audio-review-"+Guid.NewGuid().ToString("N"));public Plugin P=null!;
 public User U1=new(),U2=new();public Season Season=new(){InternalId=100},S2=new(){InternalId=200};
 public Episode E1,E2,Ambiguous,MultiSource,Active,OtherSeason;public Lib Lib=new();public Data Data=new();public SessionStub Sessions=new();
 public Fixture(){
  E1=Ep(101,Season,6);E2=Ep(102,Season,3);Ambiguous=Ep(103,Season,6);MultiSource=Ep(104,Season,6);Active=Ep(105,Season,6);OtherSeason=Ep(201,S2,6);
  Ambiguous.Sources[0].MediaStreams.Add(new(){Index=7,Type=MediaStreamType.Audio,Title="HDRezka (Dub)",Language="rus"});
  MultiSource.Sources.Add(new(){Id="alternate"});
  Lib.Items.AddRange(new BaseItem[]{Season,S2,E1,E2,Ambiguous,MultiSource,Active,OtherSeason});
  Sessions.List.Add(new(){UserId=U2.Id.ToString(),NowPlayingItem=new(){Id=Active.InternalId.ToString()}});
  Start();
 }
 public void Start(){P=new(new Paths(Dir),new Xml(),new Json(),Lib,new Users(U1,U2),Data,Sessions,new Logs());P.Configuration.Enable=true;P.Configuration.AllowedUserIds=new[]{U1.Id.ToString()};}
 public static Episode Ep(long id,Season s,int index)=>new(){InternalId=id,Season=s,Sources=new(){new(){Id="mediasource_"+id,MediaStreams=new(){new(){Index=1,Type=MediaStreamType.Audio,Title="HDRezka",Language="rus"},new(){Index=index,Type=MediaStreamType.Audio,Title="HDRezka (Dub)",Language="rus"}}}}};
 public void Raise(Episode e,int index,ProgressEvent ev,bool automated,string? source=null)=>Sessions.Raise(new(){Item=e,Session=new(){UserId=U1.Id.ToString(),UserInternalId=1},PlaySession=new(){PlayState=new(){AudioStreamIndex=index}},MediaSourceId=source??e.Sources[0].Id,EventName=ev,IsAutomated=automated});
 public void Dispose(){P.Dispose();Directory.Delete(Dir,true);}
}
sealed class Paths(string dataPath):MediaBrowser.Common.Configuration.IApplicationPaths {public string DataPath=>dataPath;}
sealed class Xml:MediaBrowser.Model.Serialization.IXmlSerializer{}
sealed class Json:MediaBrowser.Model.Serialization.IJsonSerializer {public string SerializeToString<T>(T o)=>JsonSerializer.Serialize(o);public T DeserializeFromString<T>(string s)=>JsonSerializer.Deserialize<T>(s)!;}
sealed class Logs:MediaBrowser.Model.Logging.ILogManager,MediaBrowser.Model.Logging.ILogger {public MediaBrowser.Model.Logging.ILogger GetLogger(string n)=>this;public void ErrorException(string s,Exception e)=>Console.WriteLine("[expected/diagnostic] "+s+" "+e.Message);public void Info(string s,params object?[] o){} }
sealed class Lib:ILibraryManager {public List<BaseItem> Items=new();public event EventHandler<ItemChangeEventArgs>? ItemAdded;public event EventHandler<ItemChangeEventArgs>? ItemUpdated;public BaseItem? GetItemById(long id)=>Items.SingleOrDefault(i=>i.InternalId==id);public IEnumerable<BaseItem> GetItemList(InternalItemsQuery q)=>Items.OfType<Episode>().Where(e=>e.Season==q.Parent);public void RaiseAdded(Episode e)=>ItemAdded?.Invoke(this,new(){Item=e});}
sealed class Users(params User[] list):IUserManager{public User? GetUserById(Guid id)=>list.SingleOrDefault(u=>u.Id==id);}
sealed class SessionStub:ISessionManager{public List<SessionInfo> List=new();public IEnumerable<SessionInfo> Sessions=>List;public event EventHandler<PlaybackProgressEventArgs>? PlaybackProgress;public void Raise(PlaybackProgressEventArgs e)=>PlaybackProgress?.Invoke(this,e);}
sealed class Data:IUserDataManager {
 public Dictionary<(Guid,long),UserItemData> Rows=new();public int Saves;public bool ThrowBeforeSaveOnce,ThrowAfterSaveOnce;
 public Action<User,BaseItem,int,UserItemData>? OnRead;public Dictionary<(Guid,long),int> Reads=new();
 public UserItemData GetUserData(User u,BaseItem e){var k=(u.Id,e.InternalId);if(!Rows.ContainsKey(k))Rows[k]=new();Reads[k]=Reads.GetValueOrDefault(k)+1;OnRead?.Invoke(u,e,Reads[k],Rows[k]);return Rows[k];}
 public void SaveUserData(User u,BaseItem e,UserItemData d,UserDataSaveReason reason,CancellationToken ct){if(ThrowBeforeSaveOnce){ThrowBeforeSaveOnce=false;throw new InvalidOperationException("simulated crash before write");}Saves++;Rows[(u.Id,e.InternalId)]=JsonSerializer.Deserialize<UserItemData>(JsonSerializer.Serialize(d))!;if(ThrowAfterSaveOnce){ThrowAfterSaveOnce=false;throw new InvalidOperationException("simulated crash after write");}}
}
