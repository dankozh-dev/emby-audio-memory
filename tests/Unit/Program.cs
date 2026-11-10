using System;
using Emby.AudioMemory;

var count = 0;
void Check(bool value, string description)
{
    if (!value) throw new Exception(description);
    count++;
}
Check(TrackMatch.Matches(" HDRezka (Dub) ", "rus", "hdrezka (dub)", "RUS"), "case and outer spaces");
Check(TrackMatch.Matches("HDRezka\u00a0 (Dub)", "rus", "HDRezka (Dub)", "rus"), "Unicode spaces");
Check(TrackMatch.Matches("Cafe\u0301", "rus", "Café", "rus"), "Unicode canonical equivalence");
Check(!TrackMatch.Matches("HDRezka", "rus", "HDRezka (Dub)", "rus"), "dubbing must remain distinct");
Check(!TrackMatch.Matches("HDRezka 18+", "rus", "HDRezka", "rus"), "18+ must remain distinct");
Check(!TrackMatch.Matches("HDRezka (Commentary)", "rus", "HDRezka", "rus"), "commentary distinct");
Check(!TrackMatch.Matches("HDRezka (Dub)", "eng", "HDRezka (Dub)", "rus"), "language mismatch");
Check(!TrackMatch.Matches("", "rus", "", "rus"), "missing titles fail closed");
Check(!TrackMatch.Matches("HDRezka", null, "HDRezka", null), "missing language fails closed");
Check(!TrackMatch.Matches("HDr 18+", "rus", "HDRezka 18+", "rus"), "no implicit aliases");
var tracks = new[] {
    new AudioTrack { Index = 1, Title = "HDRezka", Language = "rus" },
    new AudioTrack { Index = 2, Title = "Original", Language = "eng" },
    new AudioTrack { Index = 6, Title = "HDRezka (Dub)", Language = "rus" }
};
Check(TrackMatch.UniqueIndex(tracks,"HDRezka (Dub)","rus") == 6,"multiple Russian dubs");
tracks[2].Index = 3;
Check(TrackMatch.UniqueIndex(tracks,"HDRezka (Dub)","rus") == 3,"changed track order");
tracks[0].Title = "HDRezka (Dub)";
Check(TrackMatch.UniqueIndex(tracks,"HDRezka (Dub)","rus") == null,"ambiguous same-title tracks skipped");
Check(TrackMatch.UniqueIndex(tracks,"Missing", "rus") == null,"absent preferred dub skipped");
Check(TrackMatch.UserKey("11111111-2222-4333-8444-555555555555") == "11111111222243338444555555555555", "user GUID normalization");
Check(TrackMatch.UserKey("tv") == "", "user name cannot accidentally grant scope");
var backup = new Backup { OriginalIndex = null, LastWrittenIndex = 6, Pending = true, PendingIndex = 5 };
Check(BackupRules.Owns(backup,6), "crash before second userdata write preserves previous ownership");
Check(BackupRules.Owns(backup,5), "crash after second userdata write preserves pending ownership");
Check(!BackupRules.Owns(backup,2), "later independent choice is never restored");
Check(!BackupRules.CanReconcile(backup,2), "reconciliation preserves independent choice");
Check(BackupRules.CanReconcile(null,null), "new unselected episode is eligible");
Check(!BackupRules.CanReconcile(null,2), "unowned saved selection is preserved");
backup = new Backup { OriginalIndex = null, Pending = true, PendingIndex = 6 };
Check(!BackupRules.Owns(backup,null), "first pending write does not claim an unwritten null");
Check(BackupRules.CanReconcile(backup,null), "first pending write can retry on unchanged empty selection");
var indexed = new[] {
    new AudioTrack { Index = 1, Title = "Other", Language = "rus" },
    new AudioTrack { Index = 6, Title = null, Language = null },
    new AudioTrack { Index = 9, Title = "HDRezka (Dub)", Language = "rus" }
};
Check(TrackMatch.SelectIndex(indexed,"", "rus", 6)==6,"empty source title uses actual index");
Check(TrackMatch.SelectIndex(indexed,null, null, 6)==6,"null title and language use index");
Check(TrackMatch.SelectIndex(indexed," \t\n", "", 6)==6,"whitespace title uses index");
Check(TrackMatch.SelectIndex(indexed,"", "rus", 3)==null,"index is not the third audio-list position");
Check(TrackMatch.SelectIndex(indexed,"", "rus", -1)==null,"negative index is not selected");
Check(TrackMatch.SelectIndex(indexed,"HDRezka (Dub)", "rus", 6)==9,"named match takes precedence over index fallback");
Check(TrackMatch.SelectIndex(indexed,"LostFilm", "rus", 6)==6,"missing destination title falls back to source index");
Check(TrackMatch.SelectIndex(indexed,"LostFilm", "rus", 1)==null,"conflicting named destination does not fall back");
indexed[1].Title="Renamed studio"; indexed[1].Language="eng";
Check(TrackMatch.SelectIndex(indexed,"", "rus", 6)==6,"index mode retains number despite different title and language");
Check(TrackMatch.SelectIndex(indexed,"LostFilm", "rus", 6)==null,"named mode does not silently pick different title");
indexed[0].Title="HDRezka (Dub)";
Check(TrackMatch.SelectIndex(indexed,"HDRezka (Dub)", "rus", 9)==null,"ambiguous named match cannot fall back");
Check(TrackMatch.SelectIndex(new[]{indexed[1],indexed[1]},"", "", 6)==null,"duplicate stream indices rejected");
var ranked = new[] {
 new AudioTrack { Index=1,Title="TVShows",Language="rus",IsDefault=true },
 new AudioTrack { Index=2,Title="DUB | HDRezka Studio 18+",Language="rus" },
 new AudioTrack { Index=3,Title="MVO | HDRezka Studio",Language="rus" },
 new AudioTrack { Index=4,Title="HDRezka (Dub)",Language="rus" },
 new AudioTrack { Index=5,Title="DUB, WinMedia",Language="ukr" }
};
PriorityRule StudioRule(string name,bool adult=false)=>new PriorityRule{Studio=name,Adult=adult};
PriorityProfile Rank(params PriorityRule[] rules)=>new PriorityProfile{Language="rus",Rules=rules};
Check(PriorityMatch.SelectIndex(ranked,Rank(StudioRule("hdrezka"),StudioRule("tvshows")))==3,"studio priority outranks default flag and excludes 18+");
Check(PriorityMatch.SelectIndex(ranked,Rank(StudioRule("hdrezka",true)))==2,"adult studio is a separate choice");
Check(PriorityMatch.SelectIndex(ranked,Rank(new PriorityRule{Kind="dub"}))==4,"any dub excludes adult and wrong language tracks");
Check(PriorityMatch.SelectIndex(ranked,Rank(new PriorityRule{Kind="adult"}))==2,"any adult selects marked audio");
Check(PriorityMatch.SelectIndex(ranked,Rank(StudioRule("missing"),StudioRule("tvshows")))==1,"absent studio falls through to next rule");
Check(PriorityMatch.SelectIndex(ranked,Rank())==null,"empty list does not select audio");
ranked[3].IsDefault=true;
Check(PriorityMatch.SelectIndex(ranked,Rank(StudioRule("hdrezka")))==4,"within a rule prefer default then stream index");
Check(PriorityMatch.SelectIndex(ranked,new PriorityProfile{Language="uk",Rules=new[]{new PriorityRule{Kind="dub"}}})==5,"explicit language aliases supported");
Check(PriorityMatch.SelectIndex(ranked,new PriorityProfile{Language="",Rules=new[]{new PriorityRule{Kind="adult"}}})==null,"empty language cannot match all languages");
foreach(var title in new[]{"Dub [HDRezka]", "HDrezka (D)", "Дубляж исправленный", "Профессиональный дублированный", "Дублирование", "DUB, WinMedia"})
 Check(PriorityMatch.IsDub(title),"dub marker recognized: "+title);
foreach(var title in new[]{"MVO HDRezka", "Dublin Studio", "DVO", "DTS", "HDRezka", "D"})
 Check(!PriorityMatch.IsDub(title),"not guessed to be a dub: "+title);
foreach(var title in new[]{"HDr", "HDR", "AC3 | HDRezka Studio", "hdrezka"})
 Check(PriorityMatch.SelectIndex(new[]{new AudioTrack{Index=7,Title=title,Language="ru"}},Rank(StudioRule("hdrezka")))==7,"explicit alias: "+title);
Check(PriorityMatch.SelectIndex(new[]{new AudioTrack{Index=7,Title="NotHDRezka",Language="rus"}},Rank(StudioRule("hdrezka")))==null,"studio boundary avoids substring false positives");
Check(PriorityMatch.SelectIndex(new[]{new AudioTrack{Index=7,Title="Studio [A]+",Language="rus"}},Rank(new PriorityRule{Kind="custom",Studio="Studio [A]+"}))==7,"custom studio is literal text not regex");
Check(PriorityMatch.SelectIndex(new[]{new AudioTrack{Index=7,Title="Studio [A]+ 18+",Language="rus"}},Rank(new PriorityRule{Kind="custom",Studio="Studio [A]+"}))==null,"custom studio also excludes adult tracks");
Check(!PriorityMatch.IsAdult("118+"),"adult marker does not match 118+");
Check(PriorityMatch.IsAdult("HDRezka (18 +)"),"adult marker tolerates whitespace and parentheses");
Check(PriorityMatch.SelectIndex(new[]{new AudioTrack{Index=7,Title="",Language="rus"}},Rank(new PriorityRule{Kind="dub"},new PriorityRule{Kind="adult"}))==null,"unnamed title cannot be guessed into a priority");

Console.WriteLine($"{count} checks passed");
