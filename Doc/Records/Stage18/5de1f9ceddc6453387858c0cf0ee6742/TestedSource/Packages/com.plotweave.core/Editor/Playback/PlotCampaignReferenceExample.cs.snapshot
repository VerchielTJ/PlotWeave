using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using PlotWeave.Chapters;
using PlotWeave.Legacy;
using PlotWeave.Playback;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlotWeave.Authoring.Playback
{
    /// <summary>完全合成的公开章节样例。只构造新UGUI、文本和纹理，不读取本地原项目。</summary>
    public static class PlotCampaignReferenceExample
    {
        public const string Root="Assets/PlotWeaveMigration/Stage18_CampaignReference";
        public const string ScenePath=Root+"/CampaignReference.unity";
        public const string CampaignPath=Root+"/Campaign.asset";
        public const string ContentPath=Root+"/Content.asset";
        public const string CatalogPath=Root+"/Visuals.asset";
        private const string ManifestName=".plotweave-reference-manifest.json";
        private static readonly Color Ink=new Color(.035f,.055f,.095f),Card=new Color(.075f,.12f,.19f),Accent=new Color(.12f,.52f,.7f);
        [Serializable] public sealed class OwnedFile {public string path,sha256;}
        [Serializable] public sealed class Ownership {public int version=1;public List<OwnedFile> files=new List<OwnedFile>();}
        [Serializable] public sealed class Report {public string id,status,error,root=Root,scene=ScenePath;public bool reused,readsOriginalContent;public int chapters,episodes,rewards,nodes;public List<OwnedFile> files=new List<OwnedFile>();}

        [MenuItem("Tools/PlotWeave/迁移样例/创建公开合成章节与奖励样例")]
        public static void PrepareVerification()
        {
            var report=new Report{id=Guid.NewGuid().ToString("N"),status="preparing",readsOriginalContent=false};
            var previous=SceneManager.GetActiveScene();Scene scene=default;var working=Root+"_Preparing_"+report.id;var published=false;
            try
            {
                if(Directory.Exists(Root))
                {
                    report.files=VerifyOwnedAssets();ValidatePublicDependencies(ScenePath);report.reused=true;report.status="prepared-runtime-verification-pending";SetCounts(report);return;
                }
                Directory.CreateDirectory(working);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
                var document=CreateDocument();var catalogData=CreateCatalog();catalogData.Validate(document);
                var font=CreateFont(working,document,catalogData);var visuals=ScriptableObject.CreateInstance<PlotPlaybackVisualCatalog>();visuals.fontAsset=font;visuals.playerConfig=new PlotPlayerConfig{Positions=new[]{Vector2.zero},TextSpeed=.025f,AutoDelay=.5f,CurtainFadeTime=.35f,CurtainDisplayTime=.15f};
                foreach(var tuple in new[]{("blue",new Color(.09f,.25f,.4f),0),("green",new Color(.13f,.35f,.29f),0),("gold",new Color(.4f,.29f,.12f),0),("guide",new Color(.22f,.7f,.86f),1),("coin",new Color(.96f,.71f,.23f),2),("gem",new Color(.28f,.83f,.78f),3)})
                    visuals.sprites.Add(new PlotPlaybackSpriteBinding{key="reference."+tuple.Item1,sprite=CreateSprite(working,tuple.Item1,tuple.Item2,tuple.Item3)});
                var templates=CreateTemplates(font);try{foreach(var entry in templates)SavePrefab(entry.Value,working+"/"+entry.Key+".prefab");}finally{foreach(var root in templates.Values)UnityEngine.Object.DestroyImmediate(root);}
                var popup=CreateSkipPopup(font);try{SavePrefab(popup,working+"/SkipPopup.prefab");}finally{UnityEngine.Object.DestroyImmediate(popup);}
                visuals.viewPrefab=CreatePlayerPrefab(working,font,AssetDatabase.LoadAssetAtPath<GameObject>(working+"/SkipPopup.prefab"),visuals.playerConfig);
                var campaign=ScriptableObject.CreateInstance<PlotCampaignAsset>();campaign.data=catalogData;AssetDatabase.CreateAsset(campaign,working+"/Campaign.asset");var content=ScriptableObject.CreateInstance<PlotPlaybackAsset>();content.data=document;AssetDatabase.CreateAsset(content,working+"/Content.asset");AssetDatabase.CreateAsset(visuals,working+"/Visuals.asset");
                var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().backgroundColor=Ink;
                var browser=new GameObject("Public Campaign Reference",typeof(PlotPlayerHost),typeof(PlotLegacyCampaignHost)).GetComponent<PlotLegacyCampaignHost>();ConfigureBrowser(browser,campaign,content,visuals,working);EditorSceneManager.SaveScene(scene,working+"/CampaignReference.unity");
                AssetDatabase.SaveAssets();EditorSceneManager.CloseScene(scene,true);scene=default;if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                var moveError=AssetDatabase.MoveAsset(working,Root);if(moveError!="")throw new IOException("公开样例目录发布失败: "+moveError);published=true;AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);AssetDatabase.SaveAssets();
                ValidatePublicDependencies(ScenePath);var ownership=new Ownership{files=Directory.GetFiles(Root,"*",SearchOption.AllDirectories).Where(p=>Path.GetFileName(p)!=ManifestName).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>new OwnedFile{path=p.Substring(Root.Length+1).Replace('\\','/'),sha256=Hash(p)}).ToList()};File.WriteAllText(Root+"/"+ManifestName,JsonUtility.ToJson(ownership,true));
                report.files=ownership.files;SetCounts(report);report.status="prepared-runtime-verification-pending";
            }
            catch(Exception error)
            {
                report.status="failed";report.error=error.ToString();throw;
            }
            finally
            {
                if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                // working由本次随机ID创建，失败时只清理这个新目录；已发布目录及任何已有目标均保留。
                if(!published&&Directory.Exists(working))
                {var absolute=Path.GetFullPath(working);var expected=Path.GetFullPath(Root+"_Preparing_"+report.id);var assets=Path.GetFullPath("Assets")+Path.DirectorySeparatorChar;if(absolute==expected&&absolute.StartsWith(assets,StringComparison.OrdinalIgnoreCase))AssetDatabase.DeleteAsset(working);}
                Directory.CreateDirectory("Doc/Records/Stage18");File.WriteAllText("Doc/Records/Stage18/reference-"+report.id+".json",JsonUtility.ToJson(report,true));
            }
        }
        private static void SetCounts(Report report){var data=CreateCatalog();report.chapters=data.chapters.Count;report.episodes=data.episodes.Count;report.rewards=data.rewards.Count;report.nodes=CreateDocument().Nodes.Count;}
        private static string Hash(string path){using(var hash=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        public static List<OwnedFile> VerifyOwnedAssets()
        {
            var path=Root+"/"+ManifestName;if(!File.Exists(path))throw new IOException("同名公开样例目录已存在但没有本工具所有权清单，未覆盖");var manifest=JsonUtility.FromJson<Ownership>(File.ReadAllText(path));if(manifest==null||manifest.version!=1||manifest.files==null||manifest.files.Count<10)throw new FormatException("公开样例所有权清单无效");var root=Path.GetFullPath(Root)+Path.DirectorySeparatorChar;var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var file in manifest.files){if(string.IsNullOrEmpty(file.path)||!names.Add(file.path))throw new FormatException("公开样例清单路径缺失/重复");var full=Path.GetFullPath(Path.Combine(Root,file.path));if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!File.Exists(full)||Hash(full)!=file.sha256)throw new IOException("公开样例已有修改或缺失，未覆盖: "+file.path);}if(!File.Exists(ScenePath))throw new IOException("公开样例场景缺失");return manifest.files;
        }
        public static void ValidatePublicDependencies(string scenePath)
        {foreach(var path in AssetDatabase.GetDependencies(scenePath,true))if(path.Contains("/PlotWeaveLocal/")||path.Contains("/PlotWeaveMigration/Inputs/")||path.Replace('\\','/').StartsWith("I:/model",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("公开样例误引本地原项目: "+path);}

        public static PlotCampaignCatalog CreateCatalog()
        {
            var data=new PlotCampaignCatalog();
            data.chapters.Add(new PlotCampaignChapter{kind=PlotChapterKind.Main,id=10,name="风之书",desc="循着海风，找到第一盏灯。",episodes=new List<int>{100,500},unlockArea="studio",thumbnail="reference.blue",background="reference.blue"});
            data.chapters.Add(new PlotCampaignChapter{kind=PlotChapterKind.Main,id=80,name="未来来信",desc="这封信将在未来开启。",episodes=new List<int>{900},unlockDate="2099-1-1",thumbnail="reference.green",background="reference.green"});
            data.chapters.Add(new PlotCampaignChapter{kind=PlotChapterKind.Main,id=90,name="星光档案",desc="解锁观测站后阅读。",episodes=new List<int>{1500},unlockArea="observatory",thumbnail="reference.gold",background="reference.gold"});
            data.chapters.Add(new PlotCampaignChapter{kind=PlotChapterKind.Daily,id=20,name="午后短篇",desc="随时可以重读的小故事。",episodes=new List<int>{700,1200},unlockDate="2020-1-1",thumbnail="reference.gold",background="reference.gold"});
            data.episodes.Add(Episode(PlotChapterKind.Main,100,"第一盏灯","向导邀请你点亮工作室。",1101,101));data.episodes.Add(Episode(PlotChapterKind.Main,500,"岔路的礼物","选择一条喜欢的小路。",2401,303));
            data.episodes.Add(Episode(PlotChapterKind.Main,900,"尚未寄出的信","日期到达后开启。",1101,303));data.episodes.Add(Episode(PlotChapterKind.Main,1500,"观测站之夜","新的区域里还有新的故事。",2401,303));
            data.episodes.Add(Episode(PlotChapterKind.Daily,700,"一杯茶的时间","给忙碌留一个短暂的停顿。",8101,101));data.episodes.Add(Episode(PlotChapterKind.Daily,1200,"给明天的便笺","故事会在下一页继续。",9201,909));
            data.rewards.Add(new PlotCampaignReward{id=101,items=new List<PlotCampaignItem>{new PlotCampaignItem{itemID=7,count=3},new PlotCampaignItem{itemID=9,count=1}}});data.rewards.Add(new PlotCampaignReward{id=303,items=new List<PlotCampaignItem>{new PlotCampaignItem{itemID=7,count=5}}});data.rewards.Add(new PlotCampaignReward{id=909,items=new List<PlotCampaignItem>{new PlotCampaignItem{itemID=9,count=2}}});data.Validate();return data;
        }
        private static PlotCampaignEpisode Episode(PlotChapterKind kind,int id,string name,string desc,int entry,int award)=>new PlotCampaignEpisode{kind=kind,id=id,name=name,desc=desc,entryNode=entry,awardID=award,topTitle=kind==PlotChapterKind.Main?"风之书":"午后短篇",bottomTitle=name,skipKey="reference.skip"};
        public static PlotPlaybackDocument CreateDocument()
        {
            var data=new PlotPlaybackDocument();data.Actors.Add(new PlotPlaybackActor{ID=17,Name="reference.actor",Thespian="reference.guide"});data.Constants.Add(Text("reference.actor","织梦员","織夢員","Weaver","織り手","이야기꾼"));data.Constants.Add(Text("reference.skip","跳过这段故事并返回书架？","跳過這段故事並返回書架？","Skip this story and return?","物語をスキップしますか？","이 이야기를 건너뛸까요?"));data.Dialogs.Add(Text("reference.first","欢迎来到故事工作室。每一页都从一个小小的选择开始。","歡迎來到故事工作室。每一頁都從一個小小的選擇開始。","Welcome to the story studio. Every page begins with a small choice.","物語の工房へようこそ。","이야기 작업실에 오신 것을 환영합니다."));data.Dialogs.Add(Text("reference.lamp","灯已经亮了。下次回来，也可以重看这一页。","燈已經亮了。下次回來，也可以重看這一頁。","The lamp is lit. You can revisit this page whenever you like.","灯りがともりました。また読みに来てください。","불이 켜졌습니다. 언제든 다시 읽어 보세요."));
            data.Dialogs.Add(Text("reference.fork","海边和树林都在前方。你想往哪里走？","海邊和樹林都在前方。你想往哪裡走？","The shore and the woods are ahead. Which path will you take?","海と森、どちらへ行きますか？","바다와 숲 중 어디로 갈까요?"));data.Dialogs.Add(Text("reference.sea","海风送来一枚小小的纪念章。","海風送來一枚小小的紀念章。","The sea breeze brings a small keepsake.","潮風が小さな贈り物を運びました。","바닷바람이 작은 선물을 가져왔습니다."));data.Dialogs.Add(Text("reference.woods","树叶把阳光留在你的口袋里。","樹葉把陽光留在你的口袋裡。","The leaves tuck a little sunlight into your pocket.","木の葉が光を届けてくれました。","나뭇잎이 햇살을 전해 주었습니다."));
            data.Dialogs.Add(Text("reference.tea","停下来，给自己泡一杯茶。","停下來，給自己泡一杯茶。","Pause for a moment and make yourself some tea.","少し休んで、お茶を飲みましょう。","잠시 쉬면서 차 한 잔 마셔요."));data.Dialogs.Add(Text("reference.pause","短暂的停顿，也是一种前进。","短暫的停頓，也是一種前進。","A small pause can be part of moving forward.","小さな休みも、前進の一部です。","잠깐의 휴식도 앞으로 나아가는 길입니다."));data.Dialogs.Add(Text("reference.note","在便笺上，写下明天想做的一件事。","在便箋上，寫下明天想做的一件事。","Write down one thing you would like to do tomorrow.","明日したいことを一つ書きましょう。","내일 하고 싶은 일 하나를 적어 보세요."));data.Dialogs.Add(Text("reference.tomorrow","把它夹在书里。下一页，明天见。","把它夾在書裡。下一頁，明天見。","Keep it in the book. See you on the next page tomorrow.","本にはさんで、また明日。","책에 끼워 두세요. 내일 다음 장에서 만나요."));
            data.Nodes.Add(Node(1101,"reference.first",1109,"reference.blue"));data.Nodes.Add(Node(1109,"reference.lamp",0,""));var branch=Node(2401,"reference.fork",0,"reference.green");branch.Branch.Add(new PlotBranch{Key="去海边",Target=2407});branch.Branch.Add(new PlotBranch{Key="去树林",Target=2413});data.Nodes.Add(branch);data.Nodes.Add(Node(2407,"reference.sea",0,"reference.blue"));data.Nodes.Add(Node(2413,"reference.woods",0,"reference.green"));data.Nodes.Add(Node(8101,"reference.tea",8105,"reference.gold"));data.Nodes.Add(Node(8105,"reference.pause",0,""));data.Nodes.Add(Node(9201,"reference.note",9203,"reference.green"));data.Nodes.Add(Node(9203,"reference.tomorrow",0,""));data.Validate();return data;
        }
        private static PlotPlaybackNode Node(int id,string text,int next,string background){var node=new PlotPlaybackNode{ScenesID=id,PlotRoleID=17,Dialog=text,Next=next};node.RoleAction.Add(new PlotRoleAction{RoleID=17,RolePos=0,ActionType="Default",Emote="Neutral"});if(background!="")node.PlotSetBack.Add(new PlotSetBackground{BackName=background,SetType="Default"});return node;}
        private static PlotPlaybackText Text(string key,params string[] languages)=>new PlotPlaybackText{Key=key,Languages=languages.ToList()};

        private static TMP_FontAsset CreateFont(string folder,PlotPlaybackDocument document,PlotCampaignCatalog campaign)
        {
            var source=Resources.Load<Font>("PlotWeaveFonts/NotoSansCJKsc-Regular");if(source==null)throw new InvalidOperationException("公开样例需要随PlotWeave附带的Noto字体资源");var font=TMP_FontAsset.CreateFontAsset(source,48,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048);font.name="PlotWeave Campaign Reference Font";
            var words=string.Join("",document.Dialogs.Concat(document.Constants).SelectMany(t=>t.Languages))+string.Join("",document.Nodes.SelectMany(n=>n.Branch).Select(b=>b.Key))+string.Join("",campaign.chapters.Select(c=>c.name+c.desc))+string.Join("",campaign.episodes.Select(e=>e.name+e.desc));words+="主线故事日常故事首次奖励已领取开始重看未解锁请先完成前一章节需要解锁观测站工作室返回书架帮助取消确认提示故事阅读剧情尚未完成是否跳过本节剧情剧情已结束首次完成奖励已结算已返回已取消等待结算结算未确认重试结算取消并返回道具数量旅途徽章晨光石为每一次启程留下纪念一枚保存着清晨颜色的小石头公开示例本地进度不会连接服务器点击补全继续自动开关暂停关闭播放保存风海树林未来来信故事书架选择章节每个故事都可以重读选择一篇故事短节尚可读后：；，。（）·？";words+=new string(Enumerable.Range(32,95).Select(i=>(char)i).ToArray());var seed=new string(words.Where(c=>!char.IsControl(c)).Distinct().ToArray());if(!font.TryAddCharacters(seed,out string missing)&&missing.Length>0)throw new InvalidOperationException("公开样例字体缺少字形，数量="+missing.Length);
            font.atlasPopulationMode=AtlasPopulationMode.Static;AssetDatabase.CreateAsset(font,folder+"/ReferenceFont.asset");AssetDatabase.AddObjectToAsset(font.material,font);foreach(var atlas in font.atlasTextures)if(atlas!=null)AssetDatabase.AddObjectToAsset(atlas,font);EditorUtility.SetDirty(font);AssetDatabase.SaveAssetIfDirty(font);return font;
        }
        private static Sprite CreateSprite(string folder,string name,Color color,int kind)
        {
            var width=kind==0?640:kind==1?160:64;var height=kind==0?360:kind==1?280:64;var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);var pixels=new Color[width*height];
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            {var value=Color.Lerp(color*.4f,color,.25f+.75f*y/height);value.a=1;if(kind==0){if(y<height*.2f)value*=.65f;value.a=1;}else if(kind==1){var dx=(x-width*.5f)/(width*.25f);var dy=(y-height*.72f)/(height*.16f);value.a=dx*dx+dy*dy<1||y<height*.6f&&Mathf.Abs(x-width*.5f)<width*(.16f+.2f*(1-y/(float)height))?1:0;}else{var dx=(x-width*.5f)/(width*.42f);var dy=(y-height*.5f)/(height*.42f);value.a=(kind==2?dx*dx+dy*dy<1:Mathf.Abs(dx)+Mathf.Abs(dy)<1)?1:0;}pixels[y*width+x]=value;}
            texture.SetPixels(pixels);texture.Apply();var path=folder+"/"+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static void ConfigureBrowser(PlotLegacyCampaignHost browser,PlotCampaignAsset campaign,PlotPlaybackAsset content,PlotPlaybackVisualCatalog visuals,string folder)
        {
            browser.GetComponent<PlotPlayerHost>().playOnStart=false;browser.campaign=campaign;browser.content=content;browser.visuals=visuals;browser.chapterPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/ChapterPanel.prefab");browser.episodePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/EpisodePanel.prefab");browser.chapterItemPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/ChapterCard.prefab");browser.episodeItemPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/EpisodeCard.prefab");browser.rewardItemPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/RewardCard.prefab");browser.persistProgress=false;browser.saveFile="plotweave-public-reference.json";browser.unlockedAreas=new List<string>{"studio"};
            browser.itemPresentations=new List<PlotCampaignItemPresentation>{new PlotCampaignItemPresentation{id=7,name="旅途徽章",description="为每一次启程留下纪念。",icon=visuals.sprites.Single(s=>s.key=="reference.coin").sprite,quality=new Color(.62f,.4f,.13f)},new PlotCampaignItemPresentation{id=9,name="晨光石",description="一枚保存着清晨颜色的小石头。",icon=visuals.sprites.Single(s=>s.key=="reference.gem").sprite,quality=new Color(.12f,.48f,.46f)}};
        }
        private static void SavePrefab(GameObject root,string path){PrefabUtility.SaveAsPrefabAsset(root,path,out var success);if(!success)throw new IOException("合成模板保存失败: "+path);}
        public static Dictionary<string,GameObject> CreateTemplates(TMP_FontAsset font)
        {return new Dictionary<string,GameObject>{{"ChapterPanel",ChapterPanel(font)},{"EpisodePanel",EpisodePanel(font)},{"ChapterCard",ChapterCard(font)},{"EpisodeCard",EpisodeCard(font)},{"RewardCard",RewardCard(font)}};}
        private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
        {var go=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)go.transform;rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=low;rect.offsetMax=high;return rect;}
        private static RectTransform Fill(string name,Transform parent)=>Rect(name,parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        private static RectTransform Box(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size)
        {var rect=Rect(name,parent,anchor,anchor,Vector2.zero,Vector2.zero);rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=pos;return rect;}
        private static Image Image(RectTransform rect,Color color,bool raycast=false){var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return image;}
        private static TMP_Text Label(string name,Transform parent,string text,TMP_FontAsset font,int size,Vector2 min,Vector2 max,Vector2 low,Vector2 high,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
        {var rect=Rect(name,parent,min,max,low,high);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.fontSize=size;label.color=Color.white;label.text=text;label.alignment=align;label.raycastTarget=false;label.enableWordWrapping=true;return label;}
        private static Button Button(string name,Transform parent,string title,TMP_FontAsset font,Vector2 anchor,Vector2 pos,Vector2 size)
        {var rect=Box(name,parent,anchor,pos,size);var image=Image(rect,Accent,true);var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;Label("Title",rect,title,font,25,Vector2.zero,Vector2.one,new Vector2(12,4),new Vector2(-12,-4),TextAlignmentOptions.Center);return button;}
        private static GameObject PanelRoot(string name,Color color){var go=new GameObject(name,typeof(RectTransform));go.SetActive(false);var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(1920,1080);Image(rect,color);return go;}
        private static void ReturnBar(Transform parent,TMP_FontAsset font)
        {var bar=Rect("ReturnBtn",parent,new Vector2(0,1),Vector2.one,new Vector2(60,-170),new Vector2(-60,-70));Button("BackBtn",bar,"返回",font,new Vector2(0,.5f),new Vector2(70,0),new Vector2(140,65));Label("Title",bar,"故事书架",font,36,new Vector2(0,0),Vector2.one,new Vector2(190,8),new Vector2(-370,-8));Button("MainBtn",bar,"书架",font,new Vector2(1,.5f),new Vector2(-240,0),new Vector2(140,65));Button("HelpBtn",bar,"帮助",font,new Vector2(1,.5f),new Vector2(-70,0),new Vector2(140,65));}
        private static ScrollRect Scroll(string name,Transform parent,Vector2 min,Vector2 max)
        {var rect=Rect(name,parent,min,max,Vector2.zero,Vector2.zero);var scroll=rect.gameObject.AddComponent<ScrollRect>();var viewport=Fill("Viewport",rect);Image(viewport,new Color(.03f,.05f,.09f,.35f),true);viewport.gameObject.AddComponent<RectMask2D>();var content=Rect("Content",viewport,new Vector2(0,1),new Vector2(0,1),Vector2.zero,Vector2.zero);content.pivot=new Vector2(0,1);scroll.viewport=viewport;scroll.content=content;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;return scroll;}
        private static GameObject ChapterPanel(TMP_FontAsset font)
        {var root=PanelRoot("ChapterPanel",Ink);ReturnBar(root.transform,font);Label("Introduction",root.transform,"每个故事都可以重读，首次完成可领取奖励。",font,26,new Vector2(.04f,.79f),new Vector2(.96f,.85f),Vector2.zero,Vector2.zero);Scroll("StoryScrollRect",root.transform,new Vector2(.04f,.12f),new Vector2(.96f,.77f));Label("Footer",root.transform,"PlotWeave · 故事书架",font,22,new Vector2(.04f,.04f),new Vector2(.96f,.1f),Vector2.zero,Vector2.zero);var transition=Image(Fill("LoadBackground",root.transform),Color.white);transition.gameObject.SetActive(false);return root;}
        private static GameObject EpisodePanel(TMP_FontAsset font)
        {
            var root=PanelRoot("EpisodePanel",Ink);Image(Fill("Back",root.transform),new Color(.1f,.2f,.3f));Image(Fill("Tint",root.transform),new Color(.015f,.025f,.05f,.7f));ReturnBar(root.transform,font);Scroll("StoryScrollRect",root.transform,new Vector2(.04f,.17f),new Vector2(.47f,.77f));var details=Rect("Details",root.transform,new Vector2(.52f,.17f),new Vector2(.96f,.77f),Vector2.zero,Vector2.zero);Image(details,new Color(.035f,.065f,.105f,.93f));
            Label("Flyleaf_Titile_txt",details,"选择一篇故事",font,34,new Vector2(.06f,.78f),new Vector2(.94f,.95f),Vector2.zero,Vector2.zero);Label("Desc",details,"",font,26,new Vector2(.06f,.49f),new Vector2(.94f,.78f),Vector2.zero,Vector2.zero,TextAlignmentOptions.TopLeft);Label("AwardTitle",details,"首次奖励",font,25,new Vector2(.06f,.36f),new Vector2(.94f,.45f),Vector2.zero,Vector2.zero);Scroll("AwardScrollRect",details,new Vector2(.06f,.06f),new Vector2(.94f,.34f));var begin=Button("SelectBtn",root.transform,"开始",font,new Vector2(.74f,.09f),Vector2.zero,new Vector2(440,75));var yes=Fill("Yse",begin.transform);Image(yes,Accent);yes.SetAsFirstSibling();var no=Fill("False",begin.transform);Image(no,new Color(.2f,.26f,.3f));no.gameObject.SetActive(false);return root;
        }
        private static GameObject ChapterCard(TMP_FontAsset font)
        {var root=PanelRoot("ChapterCard",Color.clear);root.GetComponent<Image>().raycastTarget=true;root.AddComponent<Button>().targetGraphic=root.GetComponent<Image>();var body=Box("Item",root.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(590,550));Image(body,Card);Image(Rect("Thumbnai",body,new Vector2(.04f,.43f),new Vector2(.96f,.95f),Vector2.zero,Vector2.zero),Accent);Label("PlotName",body,"故事",font,36,new Vector2(.07f,.24f),new Vector2(.93f,.4f),Vector2.zero,Vector2.zero);Label("ProgressText",body,"0%",font,28,new Vector2(.07f,.06f),new Vector2(.93f,.21f),Vector2.zero,Vector2.zero);var locked=Fill("PlotLockBack",body);Image(locked,new Color(.015f,.025f,.05f,.91f));Label("LockText",locked,"尚未解锁",font,28,new Vector2(.08f,.1f),new Vector2(.92f,.9f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);Box("Pos0",root.transform,new Vector2(.5f,.5f),new Vector2(0,-24),Vector2.zero);Box("Pos1",root.transform,new Vector2(.5f,.5f),new Vector2(0,24),Vector2.zero);return root;}
        private static GameObject EpisodeCard(TMP_FontAsset font)
        {var root=PanelRoot("EpisodeCard",Card);root.GetComponent<Image>().raycastTarget=true;root.AddComponent<Button>().targetGraphic=root.GetComponent<Image>();var selected=Fill("Select",root.transform);Image(selected,new Color(.12f,.52f,.7f,.25f));Image(Rect("SelectIcon",root.transform,new Vector2(0,.1f),new Vector2(0,.9f),new Vector2(8,0),new Vector2(16,0)),new Color(.2f,.8f,.95f));Label("PlotName",root.transform,"小节",font,30,new Vector2(.05f,.15f),new Vector2(.72f,.85f),Vector2.zero,Vector2.zero);Label("PlotNext",root.transform,"可阅读",font,22,new Vector2(.73f,.15f),new Vector2(.97f,.85f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);Label("PlotOver",root.transform,"已读",font,22,new Vector2(.73f,.15f),new Vector2(.97f,.85f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);var locked=Fill("PlotLockBack",root.transform);Image(locked,new Color(.02f,.03f,.05f,.88f));Label("LockText",locked,"尚未解锁",font,25,Vector2.zero,Vector2.one,new Vector2(24,12),new Vector2(-24,-12),TextAlignmentOptions.Center);return root;}
        private static GameObject RewardCard(TMP_FontAsset font)
        {var root=PanelRoot("RewardCard",Color.clear);root.GetComponent<Image>().raycastTarget=true;root.AddComponent<Button>().targetGraphic=root.GetComponent<Image>();Image(Rect("Back",root.transform,new Vector2(.04f,.04f),new Vector2(.96f,.96f),Vector2.zero,Vector2.zero),Card);Image(Box("Icon",root.transform,new Vector2(.5f,.64f),Vector2.zero,new Vector2(72,72)),Color.white);Label("Count",root.transform,"0",font,25,new Vector2(.08f,.08f),new Vector2(.92f,.32f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);var none=Label("None",root.transform,"?",font,34,new Vector2(.1f,.3f),new Vector2(.9f,.9f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);none.gameObject.SetActive(false);return root;}
        private static GameObject CreateSkipPopup(TMP_FontAsset font)
        {var root=PanelRoot("SkipPopup",new Color(.01f,.015f,.025f,.82f));root.GetComponent<Image>().raycastTarget=true;root.AddComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=1100;root.GetComponent<Canvas>().additionalShaderChannels=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.Normal|AdditionalCanvasShaderChannels.Tangent;var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);root.AddComponent<GraphicRaycaster>();var box=Rect("Confirmation",root.transform,new Vector2(.23f,.25f),new Vector2(.77f,.75f),Vector2.zero,Vector2.zero);Image(box,Card);Label("Title",box,"跳过这段故事？",font,34,new Vector2(.06f,.72f),new Vector2(.94f,.9f),Vector2.zero,Vector2.zero);Label("ContentText",box,"",font,27,new Vector2(.06f,.35f),new Vector2(.94f,.7f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);Button("CancelBtn",box,"取消",font,new Vector2(.27f,.17f),Vector2.zero,new Vector2(190,64));Button("ConfirmBtn",box,"确认",font,new Vector2(.73f,.17f),Vector2.zero,new Vector2(190,64));return root;}
        private static GameObject CreatePlayerPrefab(string folder,TMP_FontAsset font,GameObject popup,PlotPlayerConfig config)
        {
            var root=new GameObject("CampaignReferencePlayer",typeof(RectTransform));var pool=new PlotPlayerObjectPool();try{var view=root.AddComponent<PlotPlayerView>();view.ConfigureText(font,null);view.Initialize(new PlotPlayerDataProvider(CreateDocument()),new PlotPlayerResourceLoader(),pool,config);var bindings=view.CaptureBindings();bindings.Validate();foreach(var label in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))label.font=Resources.Load<Font>("PlotWeaveFonts/NotoSansCJKsc-Regular");var canvas=bindings.uiRoot.GetComponent<Canvas>();canvas.additionalShaderChannels=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.Normal|AdditionalCanvasShaderChannels.Tangent;var button=Button("SkipStory",bindings.uiRoot.transform,"跳过",font,new Vector2(1,1),new Vector2(-790,-30),new Vector2(120,44));var skip=root.AddComponent<PlotLegacySkipController>();skip.skipButton=button;skip.popupPrefab=popup;SavePrefab(root,folder+"/Player.prefab");return AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/Player.prefab");}finally{UnityEngine.Object.DestroyImmediate(root);pool.Dispose();}
        }
    }
}
