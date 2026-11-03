using Wormy.Core;
static class NaturalLanguageSearchChecks
{
    public static void Run()
    {
        var now=new DateTimeOffset(2026,10,12,12,0,0,TimeSpan.Zero);
        var zone=TimeZoneInfo.CreateCustomTimeZone("India fixture",TimeSpan.FromHours(5.5),"India fixture","India fixture");
        SearchQuery Parse(string text)=>SearchParser.Parse(text,now,zone);
        void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        var pdf=new FileEntry("C:/fixture/Invoices/bill.pdf","bill.pdf",6_000_000,new DateTime(2026,10,8,12,0,0,DateTimeKind.Utc)){DownloadSource="Gmail"};
        Require(Parse("pdfs from last week over 5 mb").Matches(pdf),"Website PDF/week/size example failed.");
        Require(Parse("pdfs from gmail").Matches(pdf),"Website source example failed.");
        Require(Parse("pdfs from my gmail").Matches(pdf),"Optional my source qualifier failed.");
        Require(Parse("jpgs").Matches(pdf with{Name="photo.jpeg"}),"JPEG family alias failed.");
        var video=new FileEntry("C:/fixture/Movies/trip.mp4","trip.mp4",10,new DateTime(2026,8,3,12,0,0,DateTimeKind.Utc)){DeviceOrigin="Apple iPhone 15"};
        Require(Parse("videos from my iphone in august").Matches(video),"Website device/month example failed.");
        var zip=new FileEntry("C:/fixture/archive.zip","archive.zip",2_000_000_000,now.UtcDateTime.AddMonths(-7));
        Require(Parse("zips bigger than 1gb older than 6 months").Matches(zip),"Website archive/size/age example failed.");
        Require(Parse("zips bigger than one gb older than six months").Matches(zip),"Written-number ranges failed.");
        var screenshot=new FileEntry("C:/fixture/Screenshot.png","Screenshot.png",10,now.UtcDateTime);
        Require(FileSearch.Matches(Parse("screenshots with wifi password"),screenshot,[],true),"Website screenshot/content example failed.");
        Require(!FileSearch.Matches(Parse("screenshots with wifi password"),screenshot,[],false),"Screenshot content query ignored text terms.");
        Require(Parse("saved images today").Matches(screenshot)&&Parse("images saved today").Matches(screenshot),"Reordered saved-date phrase failed.");
        Require(Parse("images downloaded today").Matches(screenshot)&&Parse("images received today").Warnings.Count>0,"Download/received date alias lost file-date transparency.");
        Require(Parse("show me the pictures that i saved today").Matches(screenshot),"Conversational filler words failed.");
        Require(Parse("images \"saved\" today").Terms.Contains("saved"),"Quoted saved term was discarded.");
        Require(Parse("spreadsheets").Matches(pdf with{Name="budget.xlsx"})&&Parse("slides").Matches(pdf with{Name="deck.pptx"}),"ZIP grouped type aliases failed.");
        Require(Parse("pdfs in Documents").Matches(pdf with{Path="C:/fixture/Documents/bill.pdf"}),"ZIP standard-folder query failed.");
        Require(FileSearch.Matches(Parse("Invoices Gmail"),pdf,[],false),"ZIP folder/source keyword matching failed.");
        Require(Parse("pdfs from example.com").Matches(pdf with{SourceHost="files.example.com"})&&!Parse("pdfs from example.com").Matches(pdf with{DownloadSource=null,SourceHost="notexample.com"}),"Recorded domain source matching failed.");
        Console.WriteLine("PASS: all five website search examples, ZIP type/folder/source aliases, reordered saved dates, conversation fillers, written numbers and quoted literals.");
    }
}

