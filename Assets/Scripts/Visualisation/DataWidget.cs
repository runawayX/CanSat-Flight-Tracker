using TMPro;
using UnityEngine;

public class DataWidget : MonoBehaviour
{
    [Header("Data Properties")]
    public DataProcessor _data;
    public DataWidgetManager _parent;

    public int _category;

    [Header("UI")]
    [SerializeField] private TMP_Text _display;
    [SerializeField] private TMP_Text _value;

    private string _suffixCache = "";

    public void Init()
    {
        string cn = CansatDataHelpers.InverseDynamicDataMappings()[_category];
        _display.text = $"{CansatDataHelpers.MeasurePropertyMap[cn]._name ?? cn}:";
        _suffixCache = CansatDataHelpers.MeasurePropertyMap[cn]._suffix;
    }

    private void FixedUpdate()
    {
        if (_data.EvaluateMeasurement(_parent.GetTime(), _category, out double v))
        _value.text = $"{v} {_suffixCache}";
    }
}
