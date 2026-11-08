using HomePharma.Core;
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("OK " + name); }
var temp = Path.Combine(Path.GetTempPath(), "medikit-tests-" + Guid.NewGuid());
Directory.CreateDirectory(temp);
try {
 var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
 var store = new PharmaStore(Path.Combine(temp,"test.db"));
 await store.InitializeAsync(() => File.ReadAllTextAsync(Path.Combine(root,"data/plantilla_medikit_home.csv")));
 Check((await store.AllAsync()).Count == 0,"Public distribution starts empty");
 var med = new Medication { Name="Example", Expiry="2030-12", Categories=["Example category"], Active=true, Amount="1 tablet", Times=["08:00"], StartDate="2026-01-01" };
 med.Validate(); await store.SaveAsync(med);
 Check((await new PharmaStore(Path.Combine(temp,"test.db")).AllAsync()).Single().Name=="Example","SQLite persists medicine");
 var csv=CsvCodec.Import(CsvCodec.Export([med]));
 Check(csv.Errors.Count==0 && csv.Medications.Single().Times.Single()=="08:00","CSV preserves treatment");
 var backup=await store.BackupAsync();await store.DeleteAsync(med.Id);await store.RestoreAsync(PharmaStore.ReadBackup(backup));
 Check((await store.AllAsync()).Count==1,"Backup restores medicine");
 var lang=new AppLanguage(Path.Combine(temp,"language.txt"));lang.Set("en");
 Check(new AppLanguage(Path.Combine(temp,"language.txt")).T("Categorías")=="Categories","English preference persists");
 Check(lang.Error("Línea 2: La fecha de compra debe ser AAAA-MM-DD.").StartsWith("Line 2: The purchase"),"Validation translates");
 Check(med.IsExpired(new DateOnly(2031,1,1)) && !med.IsExpired(new DateOnly(2030,12,31)),"Expiry includes final day");
 Check(DoseRules.Next(med,"08:00",new DateTime(2026,11,8,7,0,0))==new DateTime(2026,11,8,8,0,0),"Treatment agenda time");
 Console.WriteLine($"{checks} checks passed.");return 0;
} catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
