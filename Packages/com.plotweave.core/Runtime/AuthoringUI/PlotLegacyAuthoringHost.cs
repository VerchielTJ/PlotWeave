#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PlotWeave.Authoring;
using PlotWeave.Authoring.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ValueType=PlotWeave.Authoring.PlotEventTypeAsset.ValueType;

namespace PlotWeave.Legacy.Editor
{
    /// <summary>用共享事务模型接管原制作界面；旧组件仅提供控件引用，不执行旧程序集。</summary>
    public sealed class PlotLegacyAuthoringHost:MonoBehaviour
    {
        public PlotAuthoringProject project;
        public PlotLegacyTemplateBindings template;
        public PlotEditingSession Session {get;private set;}
        public PlotNodeView NodeView {get;private set;}
        public string LastError {get;private set;}="";
        public bool InitializationReady { get; private set; }
        public string InitializationError { get; private set; } = "";
        public bool InitializationFailed => InitializationError != "";
        private bool _initializationAttempted;
        private readonly List<PlotLegacyBehaviourProxy> _units=new List<PlotLegacyBehaviourProxy>();
        private readonly List<PlotLegacyBehaviourProxy> _listRows=new List<PlotLegacyBehaviourProxy>();
        private PlotLegacyBehaviourProxy _current,_list;
        private ScrollRect _listScroll;
        private PlotAtomDataHeaderAsset.ColumnDefinition _listColumn;
        public Action<int> Preview;
        public Action ValidatePreview,ClosePreview;
        public Action RefreshPreview,OpenDataExchange;
        public RectTransform PreviewViewport { get; private set; }
        public static Action<PlotLegacyAuthoringHost> InitializationRequested;
        private bool _rendering;
        private int? _from,_to;
        private TMP_InputField _dialogInput,_keyInput;
        private TMP_Dropdown _language;
        private RectTransform _positionGuides;
        private PlotAuthoringPopupLayout _popups;
        private int _popupNode;
        private int _selectedLanguage;
        private Func<UnsavedChoice> _choose;
        private Func<string,bool> _confirm;
        private string DraftPath {get{using(var sha=System.Security.Cryptography.SHA256.Create())return "Library/PlotWeaveRecovery/"+BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(project.nodeFolder))).Replace("-","").ToLowerInvariant()+"-legacy.json";}}
        private static List<PlotLegacyBehaviourProxy> Scripts(Component root,string name) => root.GetComponentsInChildren<PlotLegacyBehaviourProxy>(true).Where(p=>p.sourceScript.EndsWith("/"+name+".cs",StringComparison.Ordinal)).ToList();
        private static T Find<T>(Component root,string name) where T:Component => PlotLegacyWidgets.Find<T>(root,name);
        private void Start(){if(!_initializationAttempted&&Session==null&&project!=null)InitializationRequested?.Invoke(this);}
        public void Initialize(IPlotAuthoringStore store,Func<UnsavedChoice> choose=null,Func<string,bool> confirm=null)
        {
            if(_initializationAttempted||Session!=null)throw new InvalidOperationException(InitializationFailed?"制作界面初始化已失败；请退出制作模式并重新打开，以免重复绑定半初始化控件":"原布局制作宿主已经初始化");
            _initializationAttempted=true;
            try
            {
            if(store==null)throw new ArgumentNullException(nameof(store));
            project=store.Project;template=template!=null?template:GetComponent<PlotLegacyTemplateBindings>();if(template==null)throw new InvalidOperationException("缺少原布局引用");
            var originalCanvas=Find<Canvas>(template,"PlotEditorCanvas");if(originalCanvas!=null){var scaler=originalCanvas.GetComponent<CanvasScaler>();if(scaler==null)scaler=originalCanvas.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(3200,1500);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;}
            _choose=choose??(()=>UnsavedChoice.Cancel);_confirm=confirm??(_=>false);Session=new PlotEditingSession(store);
            _current=Scripts(template,"CurrentNodeEditor").Single();_list=Scripts(template,"ParameterList").Single();
            _units.AddRange(Scripts(template,"RowUnit").OrderBy(p=>p.transform.GetSiblingIndex()));
            _listRows.AddRange(Scripts(template,"EvtListParameterEditor").OrderBy(p=>p.transform.GetSiblingIndex()));foreach(var item in _listRows)item.gameObject.SetActive(false);
            _listScroll=_list.Field<ScrollRect>("LoopVerticalScrollRect");if(_listScroll==null||_listRows.Count==0||_units.Count==0)throw new InvalidOperationException("原布局缺少行模板");
            DisableContentLayout(_listScroll);_listScroll.onValueChanged.RemoveAllListeners();_listScroll.onValueChanged.AddListener(_=>RenderList());
            PlotLegacyWidgets.Click(_list.Field<Button>("AddButton"),()=>Safe(()=>{if(_listColumn!=null)Session.AddListRow(_listColumn.columnName);}));
            foreach(var creator in Scripts(template,"NodeCreater"))
            {var input=creator.Field<TMP_InputField>("NewNodeInputField");PlotLegacyWidgets.Click(creator.Field<Button>("CreatNewNodeButton"),()=>Safe(()=>{Session.Create(Parse(input.text),_choose);ReloadNodes();}));}
            var loader=Scripts(template,"NodeListLoader").Single();var from=loader.Field<TMP_InputField>("FromInputField");var to=loader.Field<TMP_InputField>("ToInputField");
            PlotLegacyWidgets.Click(loader.Field<Button>("LoadNodeButton"),()=>Safe(()=>{_from=Parse(from.text);_to=Parse(to.text);ReloadNodes();}));
            PlotLegacyWidgets.Click(loader.Field<Button>("LoadAllNodeButton"),()=>Safe(()=>{_from=_to=null;ReloadNodes();}));
            PlotLegacyWidgets.Click(_current.Field<Button>("RenameNodeButton"),()=>Safe(()=>{Session.Rename(Parse(_current.Field<TMP_InputField>("RenameInputField").text),_choose);ReloadNodes();}));
            PlotLegacyWidgets.Click(_current.Field<Button>("DeletNodeButton"),()=>Safe(()=>{if(Session.CurrentAsset!=null&&_confirm("删除当前节点资产？")){Session.Delete(_choose);ReloadNodes();}}));
            var node=Scripts(template,"PlotNodeView").Single();var scroll=node.Field<ScrollRect>("nodelistScrollRect");DisableContentLayout(scroll);
            var buttons=Scripts(template,"PlotNodeButton");if(buttons.Count==0)throw new InvalidOperationException("缺少节点按钮模板");var buttonTemplate=buttons[0].GetComponent<Button>();foreach(var item in buttons)item.gameObject.SetActive(false);
            NodeView=scroll.gameObject.AddComponent<PlotNodeView>();NodeView.Initialize(scroll,project.idColumn,id=>Safe(()=>{Session.Select(id,_choose);ReloadNodes();}),parent=>{var clone=Instantiate(buttonTemplate,parent,false);clone.gameObject.SetActive(true);return clone;});
            BindDialog();BindPreview();DockAuthoringInput();AddToolbar();
            PlotAuthoringViewportLayout.Install(this);
            _popups=GetComponent<PlotAuthoringPopupLayout>();if(_popups==null)_popups=gameObject.AddComponent<PlotAuthoringPopupLayout>();
            _popups.Initialize(PreviewViewport.GetComponentInParent<Canvas>().rootCanvas,PreviewViewport.parent as RectTransform);
            Session.SelectionChanged+=Render;Session.Changed+=Changed;
            ReloadNodes();var first=store.ListRows().FirstOrDefault();if(first!=null)Session.Select(Id(first),null);else Render();
            }
            catch(Exception error){AbortInitialization(error);throw;}
        }
        /// <summary>The bootstrap declares success after the preview/exchange delegates are wired.</summary>
        public void CompleteInitialization()
        {
            if(InitializationFailed||Session==null||NodeView==null||PreviewViewport==null||Preview==null||RefreshPreview==null||ClosePreview==null||OpenDataExchange==null)
                throw new InvalidOperationException("制作界面、会话或预览尚未完整接线");
            InitializationReady=true;
        }
        /// <summary>Fail closed without saving, discarding or deleting the user's recovery draft.</summary>
        public void AbortInitialization(Exception error)
        {
            InitializationReady=false;_initializationAttempted=true;
            if(!InitializationFailed)InitializationError=(error??new InvalidOperationException("制作界面初始化失败")).ToString();
            LastError="制作界面初始化失败，请查看Console并重新打开制作模式";
            if(_popups!=null){_popups.Close();_popups.enabled=false;}
            var close=ClosePreview;Preview=null;ValidatePreview=null;ClosePreview=null;RefreshPreview=null;OpenDataExchange=null;
            try{close?.Invoke();}catch(Exception cleanup){Debug.LogException(cleanup,this);}
            var session=Session;Session=null;
            try{session?.Dispose();}catch(Exception cleanup){Debug.LogException(cleanup,this);}
            if(NodeView!=null){NodeView.enabled=false;if(Application.isPlaying)Destroy(NodeView);else DestroyImmediate(NodeView);NodeView=null;}
            _units.Clear();_listRows.Clear();_choose=null;_confirm=null;
            // Failed input must not invoke callbacks against a disposed working session.
            var blocker=GetComponent<CanvasGroup>();if(blocker==null)blocker=gameObject.AddComponent<CanvasGroup>();blocker.interactable=false;blocker.blocksRaycasts=false;
            foreach(var raycaster in GetComponentsInChildren<GraphicRaycaster>(true))raycaster.enabled=false;
            try
            {
                var canvas=GetComponentsInChildren<Canvas>(true).FirstOrDefault(c=>c.isRootCanvas);
                if(canvas!=null)
                {
                    var bar=canvas.transform.Find("PlotWeaveMigrationToolbar");
                    if(bar==null&&template!=null){AddToolbar();bar=canvas.transform.Find("PlotWeaveMigrationToolbar");}
                    if(bar!=null)
                    {
                        var notice=bar.Find("InitializationFailure");
                        if(notice==null)notice=PlotUiFactory.Label(bar,"InitializationFailure","",34,16).transform;
                        var caption=error==null?"未知错误":error.Message;
                        PlotLegacyWidgets.Caption(notice,"初始化失败："+caption+"；请查看Console，退出后重新打开");
                        var size=notice.GetComponent<LayoutElement>();if(size!=null)size.preferredWidth=850;
                        var text=notice.GetComponent<Text>();if(text!=null)text.color=new Color(1,.45f,.35f);
                    }
                }
            }
            catch(Exception noticeError){Debug.LogWarning("PlotWeave初始化失败提示不可用: "+noticeError.Message,this);}
        }
        private static void DisableContentLayout(ScrollRect scroll)
        {foreach(var layout in scroll.content.GetComponents<LayoutGroup>())layout.enabled=false;var fitter=scroll.content.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;scroll.content.anchorMin=new Vector2(0,1);scroll.content.anchorMax=Vector2.one;scroll.content.pivot=new Vector2(.5f,1);}
        private int Id(PlotAtomRowData row)=>Parse(row.GetColumnSingleValue(project.idColumn));
        private static int Parse(string value){if(!int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var id)||id<=0)throw new FormatException("节点ID必须为正整数");return id;}
        public void Safe(Action action){if(_rendering||InitializationFailed)return;try{action();LastError="";}catch(Exception error){LastError=error.Message;Status(error.Message);}}
        private void Status(string text)=>PlotLegacyWidgets.Caption(_current?.Field<Component>("TipsText"),text);
        private void Changed()
        {
            if(Session.WorkingRow!=null&&Session.HasUnsavedChanges){Directory.CreateDirectory(Path.GetDirectoryName(DraftPath));File.WriteAllText(DraftPath,Session.ExportDraft());}
            Status(Session.HasUnsavedChanges?"有未保存修改（已保留恢复草稿）":"已保存");
        }
        public void Save(){Session.Save();Render();}
        public bool PrepareDataExchange() => Session.ResolveUnsaved(_choose);
        public void ReloadAfterImport()
        {
            if (Session.HasUnsavedChanges) throw new InvalidOperationException("请先处理当前输入再载入表格结果");
            Session.Discard();
            ReloadNodes();
        }
        public void RestoreDraft(){if(!File.Exists(DraftPath))throw new FileNotFoundException("没有可恢复草稿");var json=File.ReadAllText(DraftPath);if(Session.ResolveUnsaved(_choose))Session.RestoreDraft(json);}
        private void ReloadNodes()=>NodeView.SetRows(Session.Store.ListRows(_from,_to),Session.CurrentAsset==null?0:Id(Session.CurrentAsset));
        private void AddToolbar()
        {
            var canvas=template.GetComponentsInChildren<Canvas>(true).First(c=>c.isRootCanvas);var bar=PlotUiFactory.Rect("PlotWeaveMigrationToolbar",canvas.transform);bar.anchorMin=new Vector2(0,1);bar.anchorMax=new Vector2(1,1);bar.pivot=new Vector2(.5f,1);bar.sizeDelta=new Vector2(0,38);bar.anchoredPosition=Vector2.zero;
            var layout=bar.gameObject.AddComponent<HorizontalLayoutGroup>();layout.childForceExpandWidth=false;
            PlotUiFactory.Button(bar,"SaveAll","保存全部",()=>Safe(Save),110);PlotUiFactory.Button(bar,"RecoverDraft","恢复草稿",()=>Safe(RestoreDraft),110);PlotUiFactory.Button(bar,"Validate","校验",()=>Safe(()=>{Session.WorkingRow?.Validate().ThrowIfInvalid();Status("校验通过");}),90);
            PlotUiFactory.Button(bar,"DataExchange","当前工作区表格互转",()=>Safe(()=>OpenDataExchange?.Invoke()),210);
            if(_positionGuides!=null)PlotUiFactory.Button(bar,"PositionGuides","站位参考",()=>
            {
                _positionGuides.gameObject.SetActive(!_positionGuides.gameObject.activeSelf);
                if(_positionGuides.gameObject.activeSelf)_positionGuides.SetAsLastSibling();
            },110);
        }
        private void DockAuthoringInput()
        {
            RefreshInputDock();
            var panel=PreviewViewport.parent as RectTransform;
            // Position guides are moved exactly once; a viewport resize only refreshes input.
            _positionGuides=panel.Find("PosCtrl") as RectTransform;
            if(_positionGuides==null)return;
            _positionGuides.SetParent(PreviewViewport,false);_positionGuides.anchorMin=Vector2.zero;_positionGuides.anchorMax=Vector2.one;
            _positionGuides.offsetMin=_positionGuides.offsetMax=Vector2.zero;_positionGuides.localScale=Vector3.one;
            var x=_positionGuides.Find("XPosition") as RectTransform;var y=_positionGuides.Find("YPosition") as RectTransform;
            if(x!=null){x.anchorMin=Vector2.zero;x.anchorMax=new Vector2(1,0);x.pivot=new Vector2(.5f,0);x.sizeDelta=new Vector2(0,80);x.anchoredPosition=Vector2.zero;}
            if(y!=null){y.anchorMin=Vector2.zero;y.anchorMax=new Vector2(0,1);y.pivot=new Vector2(0,.5f);y.sizeDelta=new Vector2(80,0);y.anchoredPosition=Vector2.zero;}
            foreach(var graphic in _positionGuides.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
            _positionGuides.gameObject.SetActive(false);
        }
        public void RefreshInputDock()
        {
            if(PreviewViewport==null||template==null||_dialogInput==null)return;
            var panel=PreviewViewport.parent as RectTransform;
            var editor=Scripts(template,"DiaLogKeyEditor").Single();
            var box=Find<RectTransform>(editor,"DialogBoxBG");
            if(panel==null||box==null)throw new InvalidOperationException("原制作界面缺少输入停靠区域");
            // The imported scene reserves 420 design pixels below WindowMask: the top control
            // row and bottom field buttons keep their original positions; input uses the gap.
            box.anchorMin=box.anchorMax=new Vector2(.5f,.5f);box.pivot=new Vector2(.5f,.5f);
            box.localRotation=Quaternion.identity;box.localScale=Vector3.one;
            var canvasScale=PreviewViewport.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
            var outerHeight=Mathf.Clamp(48/Mathf.Max(.01f,canvasScale)+24,160,174);
            box.sizeDelta=new Vector2(PreviewViewport.rect.width-60,outerHeight);
            var viewportCorners=new Vector3[4];PreviewViewport.GetWorldCorners(viewportCorners);
            var left=panel.InverseTransformPoint(viewportCorners[0]).x;
            box.position=panel.TransformPoint(new Vector3(left+PreviewViewport.rect.width*.5f,panel.rect.yMin+140+outerHeight*.5f,0));
            var input=(RectTransform)_dialogInput.transform;
            input.anchorMin=Vector2.zero;input.anchorMax=Vector2.one;input.pivot=new Vector2(.5f,.5f);
            input.offsetMin=new Vector2(16,12);input.offsetMax=new Vector2(-16,-12);input.localScale=Vector3.one;
        }
        private void BindPreview()
        {
            var preview=Scripts(template,"TestPlayerButton").Single();var input=Find<TMP_InputField>(preview,"PlayerStartInputField");
            PlotLegacyWidgets.Click(Find<Button>(preview,"PlayButton"),()=>Safe(()=>
            {
                if (Session.WorkingRow == null) throw new InvalidOperationException("请先选择输入节点");
                Preview?.Invoke(string.IsNullOrWhiteSpace(input.text)?Id(Session.WorkingRow):Parse(input.text));
                Status("正在试跑当前工作副本；预览不会保存资产");
            }));
            PlotLegacyWidgets.Click(Find<Button>(preview,"RefreshPlayDataButton"),()=>Safe(()=>
            {
                if (RefreshPreview != null) RefreshPreview(); else ValidatePreview?.Invoke();
                Status("已刷新当前输入；活动预览已重新试跑，资产未保存");
            }));
            var embedded = Scripts(template,"EditorPlotPlayerUI");
            if (embedded.Count != 1 || !(embedded[0].transform.parent is RectTransform viewport))
                throw new InvalidOperationException("原制作场景缺少唯一的内嵌预览区域");
            PreviewViewport = viewport;
            embedded[0].gameObject.SetActive(false);
        }
        private void BindDialog()
        {
            var editor=Scripts(template,"DiaLogKeyEditor").Single();_dialogInput=Find<TMP_InputField>(editor,"DialogInputField");_keyInput=Find<TMP_InputField>(editor,"KeyInputField");_language=Find<TMP_Dropdown>(editor,"LanguageDropdown");
            _language.ClearOptions();_language.AddOptions(new List<string>{"简体中文","繁体中文","英语","日语","韩语"});_language.SetValueWithoutNotify(0);_language.onValueChanged.RemoveAllListeners();_language.onValueChanged.AddListener(index=>{_selectedLanguage=index;RenderDialog();});
            _dialogInput.onValueChanged.RemoveAllListeners();_dialogInput.onValueChanged.AddListener(value=>Safe(()=>{if(string.IsNullOrWhiteSpace(Session.CurrentDialogKey))throw new InvalidOperationException("请先生成或填入对话KEY");Session.SetText(false,Session.CurrentDialogKey,_selectedLanguage,value);}));
            PlotLegacyWidgets.Click(Find<Button>(editor,"GenerateKey"),()=>Safe(()=>Session.GenerateDialogKey()));
            PlotLegacyWidgets.Click(Find<Button>(editor,"FillKey"),()=>Safe(()=>Session.SetDialogKey(_keyInput.text)));
            PlotLegacyWidgets.Click(Find<Button>(editor,"DeletKEYButton"),()=>Safe(()=>{if(_confirm("删除当前对话KEY及各语言文本？"))Session.RemoveText(false,Session.CurrentDialogKey);}));
            var save=Find<Button>(editor,"SaveDialogKey");if(save==null){save=PlotUiFactory.Button(editor.transform,"SaveDialogKey","保存对话",null,120);var rect=(RectTransform)save.transform;rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(1,0);rect.anchoredPosition=new Vector2(-12,12);rect.sizeDelta=new Vector2(120,34);}PlotLegacyWidgets.Click(save,()=>Safe(Save));
            var toggle=Find<Button>(editor,"DialogBoxSwitch");var dialogBox=Find<Transform>(editor,"DialogBoxBG");if(toggle!=null&&dialogBox!=null)PlotLegacyWidgets.Click(toggle,()=>dialogBox.gameObject.SetActive(!dialogBox.gameObject.activeSelf));
        }
        private void RenderDialog(){if(_keyInput==null)return;_keyInput.SetTextWithoutNotify(Session.CurrentDialogKey);_dialogInput.SetTextWithoutNotify(Session.Dialogs?.GetDialogContent(Session.CurrentDialogKey,_selectedLanguage)??"");}
        private void Render()
        {
            if(_rendering)return;_rendering=true;
            try
            {
                var row=Session.WorkingRow;PlotLegacyWidgets.Caption(_current.Field<Component>("NowNodeName"),row==null?"未选择节点":row.GetColumnSingleValue(project.idColumn));
                if(_popupNode>0&&(row==null||Id(row)!=_popupNode)){_popups?.Close();_listColumn=null;_popupNode=0;}
                var columns=row?.headerDefinition.sheets??new List<PlotAtomDataHeaderAsset.ColumnDefinition>();
                if(_units.Count<columns.Count)_popups?.Close();
                while(_units.Count<columns.Count){var clone=Instantiate(_units[0].gameObject,_units[0].transform.parent,false);_units.Add(Scripts(clone.transform,"RowUnit").Single());}
                for(var i=0;i<_units.Count;i++)
                {
                    var unit=_units[i];unit.gameObject.SetActive(i<columns.Count);if(i>=columns.Count)continue;var column=columns[i];PlotLegacyWidgets.Caption(unit.Field<Component>("Text"),column.description+" ["+column.columnName+"]");
                    var panel=unit.Field<PlotLegacyBehaviourProxy>("ParameterEditor");if(panel==null)throw new InvalidOperationException("参数面板引用丢失");
                    var open=unit.Field<Button>("OpenButton");PlotLegacyWidgets.Click(open,()=>Safe(()=>{_popupNode=Id(Session.WorkingRow);_popups.Toggle((RectTransform)panel.transform);}));
                    var list=panel.Field<Button>("OpenListButton");list.gameObject.SetActive(column.IsList);PlotLegacyWidgets.Click(list,()=>Safe(()=>{_listColumn=column;_popupNode=Id(Session.WorkingRow);_popups.Open((RectTransform)_list.transform);RenderList();_popups.Refresh();}));
                    PlotLegacyWidgets.Click(panel.Field<Button>("SaveButton"),()=>Safe(Save));BindParameters(panel,column,0,column.IsList);
                }
                if(row==null||_listColumn==null||!columns.Contains(_listColumn)){_listColumn=null;_list.gameObject.SetActive(false);}else RenderList();RenderDialog();ReloadNodes();
            }finally{_rendering=false;}
        }
        private void BindParameters(PlotLegacyBehaviourProxy container,PlotAtomDataHeaderAsset.ColumnDefinition column,int rowIndex,bool hide)
        {
            var widgets=Scripts(container,"Parameter");var header=Session.WorkingRow.headerDefinition;var leaves=column.valueType==ValueType.CustomBean?PlotBeanLayout.Leaves(header.parameterBean,column.typeKey):null;var count=hide?0:leaves?.Count??1;
            if(widgets.Count==0&&count>0)throw new InvalidOperationException("缺少参数模板");while(widgets.Count<count){var clone=Instantiate(widgets[0].gameObject,widgets[0].transform.parent,false);widgets.Add(Scripts(clone.transform,"Parameter").Single());}
            var rows=Session.WorkingRow.GetColumnRows(column.columnName);var nodeId=Id(Session.WorkingRow);
            for(var i=0;i<widgets.Count;i++)
            {
                var widget=widgets[i];widget.gameObject.SetActive(i<count);if(i>=count)continue;var member=i;var parameter=leaves?[i].Parameter;var type=parameter?.valueType??column.valueType;var enums=leaves==null?header.parameterEnums:header.parameterBean.parameterEnums;var enumName=parameter?.enumType??column.typeKey;
                var value=rowIndex<rows.Count&&i<rows[rowIndex].values.Count?rows[rowIndex].values[i]:"";PlotLegacyWidgets.Caption(widget.Field<Component>("ParameterName"),leaves?[i].Label??column.description);
                var input=widget.Field<TMP_InputField>("ParameterInputField");var dropdown=widget.Field<TMP_Dropdown>("ParameterDropdown");input.gameObject.SetActive(type!=ValueType.CustomEnum);dropdown.gameObject.SetActive(type==ValueType.CustomEnum);
                void Set(string next){if(Session.WorkingRow==null||Id(Session.WorkingRow)!=nodeId)throw new InvalidOperationException("节点已切换，忽略过期输入");Session.SetValue(column.columnName,rowIndex,member,next);}
                if(type==ValueType.CustomEnum)
                {var keys=(enums?.GetEnumKeys(enumName)??Array.Empty<string>()).ToList();var labels=keys.Select(k=>enums.GetDisplayName(enumName,k)??k).ToList();var index=keys.IndexOf(value);if(index<0){keys.Insert(0,value);labels.Insert(0,"[未绑定] "+value);index=0;}dropdown.onValueChanged.RemoveAllListeners();dropdown.ClearOptions();dropdown.AddOptions(labels);dropdown.SetValueWithoutNotify(index);dropdown.onValueChanged.AddListener(choice=>Safe(()=>Set(keys[choice])));}
                else PlotLegacyWidgets.Input(input,value,next=>Safe(()=>Set(next)),column.columnName==project.idColumn);
            }
        }
        private void RenderList()
        {
            if(_listColumn==null||Session.WorkingRow==null)return;var count=Session.WorkingRow.GetColumnRows(_listColumn.columnName).Count;PlotLegacyWidgets.Caption(_list.Field<Component>("Title"),_listColumn.description);PlotLegacyWidgets.Caption(_list.Field<Component>("CountText"),"数量："+count);
            if(count==0)
            {
                foreach(var item in _listRows)item.gameObject.SetActive(false);
                _listScroll.content.sizeDelta=Vector2.zero;_listScroll.content.anchoredPosition=Vector2.zero;
                if(_popups!=null)_popups.Refresh();return;
            }
            // Bind one real row before measuring its GridLayoutGroup. Custom beans may
            // require more than the original two 88-unit cell rows (176 total).
            _listRows[0].gameObject.SetActive(true);BindParameters(_listRows[0],_listColumn,0,false);
            var height=PrepareListRow((RectTransform)_listRows[0].transform,_listScroll.content);
            _listScroll.content.sizeDelta=new Vector2(0,count*height);
            var position=_listScroll.content.anchoredPosition;position.y=Mathf.Clamp(position.y,0,Mathf.Max(0,count*height-_listScroll.viewport.rect.height));_listScroll.content.anchoredPosition=position;
            var visible=Mathf.Min(count,Mathf.Max(1,Mathf.CeilToInt(_listScroll.viewport.rect.height/height)+2));
            while(_listRows.Count<visible){var clone=Instantiate(_listRows[0].gameObject,_listScroll.content,false);_listRows.Add(Scripts(clone.transform,"EvtListParameterEditor").Single());}
            var first=Mathf.Clamp(Mathf.FloorToInt(_listScroll.content.anchoredPosition.y/height),0,Mathf.Max(0,count-visible));
            for(var i=0;i<_listRows.Count;i++){var view=_listRows[i];view.gameObject.SetActive(i<visible);if(i>=visible)continue;var index=first+i;var rect=(RectTransform)view.transform;rect.SetParent(_listScroll.content,false);rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,height);rect.anchoredPosition=new Vector2(0,-index*height);PlotLegacyWidgets.Caption(view.Field<Component>("RowNumberText"),(index+1).ToString(CultureInfo.InvariantCulture));var name=_listColumn.columnName;PlotLegacyWidgets.Click(view.Field<Button>("DeletButton"),()=>Safe(()=>Session.RemoveListRow(name,index)));BindParameters(view,_listColumn,index,false);LayoutRebuilder.ForceRebuildLayoutImmediate(rect);}
            if(_popups!=null)_popups.Refresh();
        }
        private static float PrepareListRow(RectTransform row,RectTransform content)
        {
            var fallback=Mathf.Max(100,row.rect.height);row.SetParent(content,false);
            row.anchorMin=new Vector2(0,1);row.anchorMax=Vector2.one;row.pivot=new Vector2(.5f,1);
            // A stretched row needs zero width delta; retaining the original 700 adds
            // another 700 units and shifts half of the two-column grid behind the mask.
            row.sizeDelta=new Vector2(0,fallback);row.anchoredPosition=Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);var preferred=LayoutUtility.GetPreferredHeight(row);
            var height=preferred>0?Mathf.Max(100,preferred):fallback;row.sizeDelta=new Vector2(0,height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);return height;
        }
        private void OnDestroy(){InitializationReady=false;ClosePreview?.Invoke();Session?.Dispose();Session=null;}
    }
}
#endif
