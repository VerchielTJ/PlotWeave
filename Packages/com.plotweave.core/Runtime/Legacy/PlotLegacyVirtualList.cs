using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Legacy
{
    public sealed class PlotLegacyVirtualList:MonoBehaviour
    {
        private ScrollRect _scroll;private GameObject _prefab;private bool _horizontal;private float _size;private int _count;private Action<GameObject,int> _bind;
        private readonly List<GameObject> _pool=new List<GameObject>();public int PoolSize=>_pool.Count;private bool _refreshing;
        private sealed class ImportedRoot {public RectTransform rect;public Vector2 size;public bool stretchX,stretchY;}
        private readonly Dictionary<GameObject,ImportedRoot> _importedRoots=new Dictionary<GameObject,ImportedRoot>();
        public void Initialize(ScrollRect scroll,GameObject prefab,bool horizontal,float size)
        {
            if(_scroll!=null)throw new InvalidOperationException("列表已经初始化");if(scroll==null||scroll.content==null||scroll.viewport==null||prefab==null||size<=0)throw new ArgumentException("虚拟列表绑定无效");
            _scroll=scroll;_prefab=prefab;_horizontal=horizontal;_size=size;foreach(var layout in scroll.content.GetComponents<LayoutGroup>())layout.enabled=false;var fitter=scroll.content.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
            for(var i=0;i<scroll.content.childCount;i++)scroll.content.GetChild(i).gameObject.SetActive(false);scroll.content.anchorMin=scroll.content.anchorMax=new Vector2(0,1);scroll.content.pivot=new Vector2(0,1);
            // 原Content的居中pivot偏移在统一左上原点后已失效，不能继续带入交叉轴。
            scroll.content.anchoredPosition=Vector2.zero;scroll.horizontal=horizontal;scroll.vertical=!horizontal;scroll.onValueChanged.RemoveAllListeners();scroll.onValueChanged.AddListener(_=>Refresh());
        }
        public void Set(int count,Action<GameObject,int> bind,int selected=0)
        {if(count<0||bind==null)throw new ArgumentException();_count=count;_bind=bind;var viewport=_scroll.viewport.rect;_scroll.content.sizeDelta=_horizontal?new Vector2(count*_size,viewport.height):new Vector2(viewport.width,count*_size);ScrollTo(selected);}
        public void ScrollTo(int index){var max=Mathf.Max(0,_count*_size-(_horizontal?_scroll.viewport.rect.width:_scroll.viewport.rect.height));var offset=Mathf.Clamp(Mathf.Clamp(index,0,Mathf.Max(0,_count-1))*_size,0,max);_scroll.content.anchoredPosition=_horizontal?new Vector2(-offset,0):new Vector2(0,offset);Refresh();}
        public void Refresh()
        {
            if(_scroll==null||_bind==null||_refreshing)return;_refreshing=true;try{var length=_horizontal?_scroll.viewport.rect.width:_scroll.viewport.rect.height;var visible=Mathf.Min(_count,Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(100,length)/_size)+2));var first=Mathf.Clamp(Mathf.FloorToInt((_horizontal?-_scroll.content.anchoredPosition.x:_scroll.content.anchoredPosition.y)/_size),0,Mathf.Max(0,_count-visible));
            while(_pool.Count<visible)
            {var item=Instantiate(_prefab,_scroll.content,false);_pool.Add(item);var binding=item.GetComponent<PlotLegacyTemplateBindings>();if(binding!=null&&!string.IsNullOrEmpty(binding.sourceLayout)&&item.transform.childCount==1&&item.transform.GetChild(0) is RectTransform imported)_importedRoots.Add(item,new ImportedRoot{rect=imported,size=imported.rect.size,stretchX=imported.anchorMin.x!=imported.anchorMax.x,stretchY=imported.anchorMin.y!=imported.anchorMax.y});}
            for(var i=0;i<_pool.Count;i++)
            {var item=_pool[i];item.SetActive(i<visible);if(i>=visible)continue;foreach(var group in item.GetComponentsInChildren<CanvasGroup>(true)){group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}var index=first+i;var rect=(RectTransform)item.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.sizeDelta=_horizontal?new Vector2(_size,_scroll.viewport.rect.height):new Vector2(_scroll.viewport.rect.width,_size);rect.anchoredPosition=_horizontal?new Vector2(index*_size,0):new Vector2(0,-index*_size);
                // 原布局的中性包装层不参与排版；把唯一源根放进实际格子，保留其固定设计尺寸。
                // 无此标记的用户模板/公开合成模板不改子布局。
                if(_importedRoots.TryGetValue(item,out var imported)){var area=rect.rect.size;imported.rect.anchorMin=imported.rect.anchorMax=imported.rect.pivot=new Vector2(.5f,.5f);imported.rect.sizeDelta=new Vector2(imported.stretchX?area.x:Mathf.Min(imported.size.x,area.x),imported.stretchY?area.y:Mathf.Min(imported.size.y,area.y));imported.rect.anchoredPosition=Vector2.zero;}
                _bind(item,index);}}
            finally{_refreshing=false;}
        }
        private void OnRectTransformDimensionsChange(){if(_scroll!=null)Refresh();}
    }
}
