using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.Threading;
using TMSpeech.Core.Plugins;

namespace TMSpeech.GUI.Controls;

public class PluginConfigView : UserControl
{
    private readonly Grid _container;

    public PluginConfigView()
    {
        _container = new AutoGrid()
        {
            RowCount = 100,
            ColumnDefinitions = new ColumnDefinitions("100,*"),
        };
        this.Content = _container;
    }

    public static readonly StyledProperty<IPluginConfigEditor?> ConfigEditorProperty =
        AvaloniaProperty.Register<PluginConfigView, IPluginConfigEditor?>(
            nameof(ConfigEditor));

    public IPluginConfigEditor? ConfigEditor
    {
        get => GetValue(ConfigEditorProperty);
        set => SetValue(ConfigEditorProperty, value);
    }

    public static readonly StyledProperty<string> ValueProperty = AvaloniaProperty.Register<PluginConfigView, string>(
        nameof(Value));

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private enum UpdateMode
    {
        ViewToBoth,
        PluginLayerToViewToValue,
        ValueToViewToPluginLayer,
    }

    private UpdateMode _updateMode = UpdateMode.ViewToBoth;

    private void UpdateValueAndNotify()
    {
        Value = ConfigEditor.GenerateConfig();
    }

    private void LoadValuesToView()
    {
        if (ConfigEditor == null) return;
        var values = ConfigEditor.GetAll();
        int controlIndex = 0;
        foreach (var formItem in ConfigEditor.GetFormItems())
        {
            // 跳过标签，找到对应的控件
            controlIndex++; // 标签
            if (controlIndex >= _container.Children.Count) break;
            
            var control = _container.Children[controlIndex];
            controlIndex++;
            
            if (!values.TryGetValue(formItem.Key, out var value)) continue;
            
            switch (control)
            {
                case TextBox tb when formItem is PluginConfigFormItemTextArea:
                    // TextArea (multi-line TextBox)
                    tb.Text = value?.ToString() ?? "";
                    tb.IsEnabled = IsFieldEnabled(formItem.Key);
                    break;
                case TextBox tb:
                    tb.Text = value?.ToString() ?? "";
                    tb.IsEnabled = IsFieldEnabled(formItem.Key);
                    break;
                case FilePicker fp:
                    fp.Text = value?.ToString() ?? "";
                    fp.IsEnabled = IsFieldEnabled(formItem.Key);
                    break;
                case ComboBox cb:
                    cb.SelectedValue = value;
                    cb.IsEnabled = IsFieldEnabled(formItem.Key);
                    break;
                case CheckBox chk:
                    chk.IsChecked = value is bool b && b;
                    chk.IsEnabled = IsFieldEnabled(formItem.Key);
                    break;
                case Slider slider:
                    if (float.TryParse(value?.ToString(), out var floatVal))
                        slider.Value = floatVal;
                    slider.IsEnabled = IsFieldEnabled(formItem.Key);
                    break;
            }
        }
    }

    // 判断字段是否应该启用
    private bool IsFieldEnabled(string fieldKey)
    {
        if (ConfigEditor == null) return true;
        
        var apiName = ConfigEditor.GetValue("ApiName")?.ToString() ?? "Google";
        
        // 通用字段始终启用
        var commonFields = new[] { "ApiName", "TargetLanguage", "TimeoutMs", "ContextAwareEnabled", "ContextCount" };
        if (commonFields.Contains(fieldKey))
            return true;
        
        // 无需配置的 API
        var noConfigApis = new[] { "Google", "Google2" };
        if (noConfigApis.Contains(apiName))
            return false;
        
        // REST API（DeepL、LibreTranslate、Youdao、Baidu、MTranServer）
        var restApis = new[] { "DeepL", "LibreTranslate", "Youdao", "Baidu", "MTranServer" };
        
        // LLM API（OpenAI、Ollama、OpenRouter）
        var llmApis = new[] { "OpenAI", "Ollama", "OpenRouter" };
        
        // REST API 和 LLM API 字段
        if (fieldKey == "ApiUrl" || fieldKey == "ApiKey")
            return restApis.Contains(apiName) || llmApis.Contains(apiName);
        
        // LLM API 字段
        var llmApiFields = new[] { "ModelName", "Temperature", "Prompt" };
        if (llmApiFields.Contains(fieldKey))
            return llmApis.Contains(apiName);
        
        // ApiConfig 字段：始终显示，但仅对需要特定配置的 API 启用（Baidu、Youdao）
        if (fieldKey == "ApiConfig")
            return apiName == "Baidu" || apiName == "Youdao";
        
        return true;
    }

    // generate controls and events
    private void GenerateControls()
    {
        _container.Children.Clear();
        if (ConfigEditor == null) return;
        foreach (var formItem in ConfigEditor.GetFormItems())
        {
            var label = new Label()
            {
                Content = formItem.Name,
            };
            _container.Children.Add(label);
            Control control;
            if (formItem is PluginConfigFormItemText)
            {
                var tb = new TextBox()
                {
                    Tag = formItem.Key
                };
                tb.TextChanged += (_, _) =>
                {
                    if (_updateMode != UpdateMode.ViewToBoth) return;

                    ConfigEditor.SetValue(formItem.Key, tb.Text);
                    UpdateValueAndNotify();
                };
                control = tb;
            }
            else if (formItem is PluginConfigFormItemFile fileFormItem)
            {
                var fp = new FilePicker()
                {
                    Tag = fileFormItem.Key,
                    Type = fileFormItem.Type == PluginConfigFormItemFileType.File
                        ? FilePickerType.File
                        : FilePickerType.Folder,
                };
                fp.FileChanged += (_, _) =>
                {
                    if (_updateMode != UpdateMode.ViewToBoth) return;
                    
                    ConfigEditor.SetValue(formItem.Key, fp.Text);
                    UpdateValueAndNotify();
                };
                control = fp;
            }
            else if (formItem is PluginConfigFormItemOption optionFormItem)
            {
                var cb = new ComboBox()
                {
                    Tag = optionFormItem.Key,
                    ItemsSource = optionFormItem.Options.ToList(),
                    SelectedValueBinding = new Binding("Key"),
                    ItemTemplate = new FuncDataTemplate<KeyValuePair<object, string>>((v, namescope) => new TextBlock()
                        {
                            [!TextBlock.TextProperty] = new Binding("Value"),
                        }
                    )
                };

                cb.SelectionChanged += (_, _) =>
                {
                    if (_updateMode != UpdateMode.ViewToBoth) return;
                    
                    ConfigEditor.SetValue(formItem.Key, optionFormItem.Options.Keys.ToList()[cb.SelectedIndex]);
                    UpdateValueAndNotify();
                };
                control = cb;
            }
            else if (formItem is PluginConfigFormItemPassword)
            {
                var pb = new TextBox()
                {
                    Tag = formItem.Key,
                    PasswordChar = '*'
                };
                pb.TextChanged += (_, _) =>
                {
                    if (_updateMode != UpdateMode.ViewToBoth) return;
                    
                    ConfigEditor.SetValue(formItem.Key, pb.Text);
                    UpdateValueAndNotify();
                };
                control = pb;
            }
            else if (formItem is PluginConfigFormItemCheckbox)
            {
                var cb = new CheckBox()
                {
                    Tag = formItem.Key
                };
                cb.IsCheckedChanged += (_, _) =>
                {
                    if (_updateMode != UpdateMode.ViewToBoth) return;
                    
                    ConfigEditor.SetValue(formItem.Key, cb.IsChecked ?? false);
                    UpdateValueAndNotify();
                };
                control = cb;
            }
            else if (formItem is PluginConfigFormItemSlider sliderFormItem)
            {
                var slider = new Slider()
                {
                    Tag = formItem.Key,
                    Minimum = sliderFormItem.Min,
                    Maximum = sliderFormItem.Max,
                    TickFrequency = sliderFormItem.Step
                };
                slider.PropertyChanged += (_, e) =>
                {
                    if (_updateMode != UpdateMode.ViewToBoth || e.Property.Name != "Value") return;
                    
                    ConfigEditor.SetValue(formItem.Key, (float)slider.Value);
                    UpdateValueAndNotify();
                };
                control = slider;
            }
            else if (formItem is PluginConfigFormItemTextArea textAreaFormItem)
            {
                var tb = new TextBox()
                {
                    Tag = formItem.Key,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    Height = 100
                };
                tb.TextChanged += (_, _) =>
                {
                    if (_updateMode != UpdateMode.ViewToBoth) return;
                    
                    ConfigEditor.SetValue(formItem.Key, tb.Text);
                    UpdateValueAndNotify();
                };
                control = tb;
            }
            else
            {
                control = new Label()
                {
                    Content = "Not supported",
                    Foreground = Brushes.Red,
                };
            }

            _container.Children.Add(control);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ConfigEditorProperty)
        {
            if (change.OldValue is IPluginConfigEditor oldConfig)
            {
                oldConfig.ValueUpdated -= OnPluginLayerConfigValueUpdated;
                oldConfig.FormItemsUpdated -= OnPluginLayerConfigFormItemsUpdated;
            }

            GenerateControls();
            if (change.NewValue is IPluginConfigEditor newConfig)
            {
                OnPluginLayerConfigValueUpdated(this, null);
                newConfig.ValueUpdated += OnPluginLayerConfigValueUpdated;
                newConfig.FormItemsUpdated += OnPluginLayerConfigFormItemsUpdated;
            }
        }
        else if (change.Property == ValueProperty)
        {
            if (_updateMode != UpdateMode.ViewToBoth) return;
            _updateMode = UpdateMode.ValueToViewToPluginLayer;
            ConfigEditor?.LoadConfigString(change.GetNewValue<string>());
            LoadValuesToView();
            _updateMode = UpdateMode.ViewToBoth;
        }
    }


    private void OnPluginLayerConfigFormItemsUpdated(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (_updateMode != UpdateMode.ViewToBoth) return;
            _updateMode = UpdateMode.PluginLayerToViewToValue;
            GenerateControls();
            LoadValuesToView();
            _updateMode = UpdateMode.ViewToBoth;
        });
    }

    private void OnPluginLayerConfigValueUpdated(object? sender, EventArgs e)
    {
        if (_updateMode != UpdateMode.ViewToBoth) return;
        _updateMode = UpdateMode.PluginLayerToViewToValue;
        UpdateValueAndNotify();
        LoadValuesToView();
        _updateMode = UpdateMode.ViewToBoth;
    }
}