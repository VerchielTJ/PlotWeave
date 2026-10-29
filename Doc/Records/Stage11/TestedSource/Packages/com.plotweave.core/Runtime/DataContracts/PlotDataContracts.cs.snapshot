using System;
using System.Collections.Generic;

namespace PlotWeave.Data
{
    public interface IPlotDataBase
    {
        bool IsValid { get; }
        string DataSource { get; }
    }
    public struct PlotDataRequest
    {
        public int ID;
        public string DataType;
        public object[] Parameters;
        public PlotDataRequest(int id, string dataType, params object[] parameters)
        { ID = id; DataType = dataType; Parameters = parameters ?? Array.Empty<object>(); }
    }
    public struct PlotDataResponse
    {
        public bool IsSuccess;
        public IPlotDataBase Data;
        public string ErrorMessage;
        public static PlotDataResponse Success(IPlotDataBase data) => new PlotDataResponse { IsSuccess = true, Data = data };
        public static PlotDataResponse Failure(string error) => new PlotDataResponse { IsSuccess = false, ErrorMessage = error };
    }
    public interface IPlotThespianData : IPlotDataBase
    {
        int ID { get; }
        string Name { get; }
        string Thespian { get; }
    }
    public interface IPlotTextData
    {
        string GetText(string key, int languageType = 0);
        bool HasKey(string key);
        string[] GetAllKeys();
    }
    public interface IPlotItemTable<TKey, TItem> where TItem : class, IPlotDataBase
    {
        TItem GetItem(TKey id);
        List<TItem> GetAllItems();
        List<TKey> GetAllIds();
        bool HasId(TKey id);
    }
    /// <summary>确定的插入顺序；同ID更新不改变顺序，不向外泄漏内部容器。</summary>
    public class PlotItemTable<TKey, TItem> : IPlotItemTable<TKey, TItem> where TItem : class, IPlotDataBase
    {
        private readonly Dictionary<TKey, TItem> _items = new Dictionary<TKey, TItem>();
        private readonly List<TKey> _order = new List<TKey>();
        public TItem GetItem(TKey id) => id is null ? null : _items.TryGetValue(id, out var item) ? item : null;
        public List<TItem> GetAllItems() { var result = new List<TItem>(); foreach (var id in _order) result.Add(_items[id]); return result; }
        public List<TKey> GetAllIds() => new List<TKey>(_order);
        public bool HasId(TKey id) => !(id is null) && _items.ContainsKey(id);
        public void AddItem(TKey id, TItem item)
        {
            if (id is null) throw new ArgumentNullException(nameof(id));
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!_items.ContainsKey(id)) _order.Add(id);
            _items[id] = item;
        }
        public void Clear() { _items.Clear(); _order.Clear(); }
    }
}
