﻿/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace OsEngine.Entity
{
    /// <summary>
    /// parameter interface
    /// интерфейс для параметра
    /// </summary>
    public interface IIStrategyParameter
    {
        /// <summary>
        /// уникальное имя параметра
        /// </summary>
        string Name { get; }

        /// <summary>
        /// unique parameter name
        /// взять строку для сохранения
        /// </summary>
        string GetStringToSave();

        /// <summary>
        /// загрузить параметр из строки
        /// загрузить параметр из строки
        /// </summary>
        /// <param name="save">line with saved parameters/строка с сохранёнными параметрами</param>
        void LoadParamFromString(string[] save);

        /// <summary>
        /// parameter type
        /// тип параметра
        /// </summary>
        StrategyParameterType Type { get; }

        /// <summary>
        /// name of the tab in the param window / 
        /// название вкладки в окне параметров
        /// </summary>
        string TabName { get; set; }

        /// <summary>
        /// the parameter state has changed
        /// изменилось состояние параметра
        /// </summary>
        event Action ValueChange;
    }

    public class StrategyParameterLabel : IIStrategyParameter
    {
        public StrategyParameterLabel(string name, string label, string value, int rowHeight, int textHeight, System.Drawing.Color color, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("название параметра у робота содержит спец-символ. Это вызовет ошибки. Уберите его");
            }

            _name = name;
            Label = label;
            Value = value;
            TabName = tabName;
            RowHeight = rowHeight;
            TextHeight = textHeight;
            Color = color;
        }

        public string Label;
        public string Value;
        public int RowHeight;
        public int TextHeight;
        public System.Drawing.Color Color;

        public string Name { get { return _name; } }
        private string _name;

        public StrategyParameterType Type { get { return StrategyParameterType.Label; } }

        public string TabName { get; set; }

        public event Action ValueChange;

        public string GetStringToSave()
        {
            string save = _name + "#";

            save += Label + "#";
            save += Value + "#";
            save += RowHeight + "#";
            save += TextHeight + "#";
            save += Color.ToArgb() + "#";

            return save;

        }

        public void LoadParamFromString(string[] save)
        {
            try
            {
                Label = save[1];
                Value = save[2];
                RowHeight = Convert.ToInt32(save[3]);
                TextHeight = Convert.ToInt32(save[4]);
                Color = System.Drawing.Color.FromArgb(Convert.ToInt32(save[5]));
            }
            catch
            {
                // ignore 
            }
        }
    }

    /// <summary>
    /// Parameter for an Int strategy
    /// параметр для стратегии типа Int
    /// </summary>
    public class StrategyParameterInt : IIStrategyParameter
    {
        /// <summary>
        /// constructor to create a parameter storing Int variables
        /// конструктор для создания параметра хранящего переменные типа Int
        /// </summary>
        /// <param name="name">Parameter name/Имя параметра</param>
        /// <param name="value">Default value/Значение по умолчанию</param>
        /// <param name="start">First value in optimization/Первое значение при оптимизации</param>
        /// <param name="stop">Last value during optimization/Последнее значение при оптимизации</param>
        /// <param name="step">Step change in optimization/Шаг изменения при оптимизации</param>
        public StrategyParameterInt(string name, int value, int start, int stop, int step, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("название параметра у робота содержит спец-символ. Это вызовет ошибки. Уберите его");
            }

            if (start > stop)
            {
                throw new Exception("Начальное значение параметра не может быть больше последнему");
            }

            _name = name;
            _valueInt = value;
            _valueIntDefolt = value;
            _valueIntStart = start;
            _valueIntStop = stop;
            _valueIntStep = step;
            TabName = tabName;
        }

        /// <summary>
        /// closed constructor
        /// закрытый конструктор
        /// </summary>
        private StrategyParameterInt()
        {

        }

        /// <summary>
        /// unique parameter name
        /// уникальное имя параметра
        /// </summary>
        public string Name
        {
            get { return _name; }
        }
        private string _name;

        public string TabName
        {
            get; set;
        }

        /// <summary>
        /// save the line
        /// взять строку сохранения
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";

            save += _valueInt + "#";
            save += _valueIntDefolt + "#";
            save += _valueIntStart + "#";
            save += _valueIntStop + "#";
            save += _valueIntStep + "#";

            return save;
        }

        /// <summary>
        /// Load the parameter from the saved file
        /// загрузить параметр из сохранённого файла
        /// </summary>
        public void LoadParamFromString(string[] save)
        {
            _valueInt = Convert.ToInt32(save[1]);

            try
            {
                _valueIntDefolt = Convert.ToInt32(save[2]);
                _valueIntStart = Convert.ToInt32(save[3]);
                _valueIntStop = Convert.ToInt32(save[4]);
                _valueIntStep = Convert.ToInt32(save[5]);
            }
            catch
            {
                // ignore 
            }

        }

        /// <summary>
        /// parameter type
        /// тип параметра
        /// </summary>
        public StrategyParameterType Type
        {
            get { return StrategyParameterType.Int; }
        }

        /// <summary>
        /// current value of the parameter of Int type
        /// текущее значение параметра типа Int
        /// </summary>
        public int ValueInt
        {
            get
            {
                return _valueInt;
            }
            set
            {
                if (_valueInt == value)
                {
                    return;
                }
                _valueInt = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }
        private int _valueInt;

        /// <summary>
        /// default value for the Int type parameter
        /// значение по умолчанию для параметра типа Int
        /// </summary>
        public int ValueIntDefolt
        {
            get
            {
                return _valueIntDefolt;
            }
        }
        private int _valueIntDefolt;

        /// <summary>
        /// starting value during optimization for the parameter of Int
        /// стартовое значение при оптимизации для параметра типа Int
        /// </summary>
        public int ValueIntStart
        {
            get
            {
                return _valueIntStart;
            }
        }
        private int _valueIntStart;

        /// <summary>
        /// the last value during optimization for the parameter of Int type
        /// последнее значение при оптимизации для параметра типа Int
        /// </summary>
        public int ValueIntStop
        {
            get
            {
                return _valueIntStop;
            }
        }
        private int _valueIntStop;

        /// <summary>
        /// incremental step for the Int type parameter 
        /// шаг приращения для параметра типа Int 
        /// </summary>
        public int ValueIntStep
        {
            get
            {
                return _valueIntStep;
            }
        }
        private int _valueIntStep;

        /// <summary>
        /// the parameter state has changed
        /// изменилось состояние параметра
        /// </summary>
        public event Action ValueChange;
    }

    /// <summary>
    /// The parameter of the Decimal type strategy
    /// параметр стратегии типа Decimal
    /// </summary>
    public class StrategyParameterDecimal : IIStrategyParameter
    {

        /// <summary>
        /// Designer for creating a parameter storing Decimal type variables
        /// конструктор для создания параметра хранящего переменные типа Decimal
        /// </summary>
        /// <param name="name">Parameter name/Имя параметра</param>
        /// <param name="value">Default value/Значение по умолчанию</param>
        /// <param name="start">First value in optimization/Первое значение при оптимизации</param>
        /// <param name="stop">last value in optimization/Последнее значение при оптимизации</param>
        /// <param name="step">Step change in optimization/Шаг изменения при оптимизации</param>
        public StrategyParameterDecimal(string name, decimal value, decimal start, decimal stop, decimal step, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("название параметра у робота содержит спец-символ. Это вызовет ошибки. Уберите его");
            }
            if (start > stop)
            {
                throw new Exception("Начальное значение параметра не может быть больше последнему");
            }

            _name = name;
            _valueDecimal = value;
            _valueDecimalDefolt = value;
            _valueDecimalStart = start;
            _valueDecimalStop = stop;
            _valueDecimalStep = step;
            _type = StrategyParameterType.Decimal;
            TabName = tabName;
        }

        /// <summary>
        /// blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// заглушка. нельзя создать переменную типа StrategyParameter с пустым конструктором
        /// </summary>
        private StrategyParameterDecimal()
        {

        }

        /// <summary>
        /// to take a line to save
        /// взять строку для сохранения
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";
            save += _valueDecimal + "#";
            save += _valueDecimalDefolt + "#";
            save += _valueDecimalStart + "#";
            save += _valueDecimalStop + "#";
            save += _valueDecimalStep + "#";

            return save;
        }

        /// <summary>
        /// download settings from the save file
        /// загрузить настройки из файла сохранения
        /// </summary>
        /// <param name="save"></param>
        public void LoadParamFromString(string[] save)
        {
            _valueDecimal = save[1].ToDecimal();

            try
            {
                _valueDecimalDefolt = save[2].ToDecimal();
                _valueDecimalStart = save[3].ToDecimal();
                _valueDecimalStop = save[4].ToDecimal();
                _valueDecimalStep = save[5].ToDecimal();
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Parameter name. Used to identify a parameter in the settings windows
        /// Название параметра. Используется для идентификации параметра в окнах настроек
        /// </summary>
        public string Name
        {
            get { return _name; }
        }
        private string _name;

        public string TabName
        {
            get; set;
        }

        /// <summary>
        /// parameter type
        /// тип параметра
        /// </summary>
        public StrategyParameterType Type
        {
            get { return _type; }
        }
        private StrategyParameterType _type;

        /// <summary>
        /// current value of the Decimal parameter
        /// текущее значение параметра Decimal
        /// </summary>
        public decimal ValueDecimal
        {
            get
            {
                return _valueDecimal;
            }
            set
            {
                if (_valueDecimal == value)
                {
                    return;
                }
                _valueDecimal = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }
        private decimal _valueDecimal;

        /// <summary>
        /// default value for the Decimal type
        /// значение по умолчанию для параметра типа Decimal
        /// </summary>
        public decimal ValueDecimalDefolt
        {
            get
            {
                return _valueDecimalDefolt;
            }
        }
        private decimal _valueDecimalDefolt;

        /// <summary>
        /// initial value of the Decimal type parameter
        /// начальное значение параметра типа Decimal
        /// </summary>
        public decimal ValueDecimalStart
        {
            get
            {
                return _valueDecimalStart;
            }
        }
        private decimal _valueDecimalStart;

        /// <summary>
        /// the last value of the Decimal type parameter
        /// последнее значение параметра типа Decimal
        /// </summary>
        public decimal ValueDecimalStop
        {
            get
            {
                return _valueDecimalStop;
            }
        }
        private decimal _valueDecimalStop;

        /// <summary>
        /// incremental step of the Decimal type parameter
        /// шаг приращения параметра типа Decimal
        /// </summary>
        public decimal ValueDecimalStep
        {
            get
            {
                return _valueDecimalStep;
            }
        }
        private decimal _valueDecimalStep;

        /// <summary>
        /// event: the parameter has changed
        /// событие: параметр изменился
        /// </summary>
        public event Action ValueChange;
    }

    /// <summary>
    /// Bool type strategy parameter
    /// параметр стратегии типа Bool
    /// </summary>
    public class StrategyParameterBool : IIStrategyParameter
    {
        public StrategyParameterBool(string name, bool value, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("название параметра у робота содержит спец-символ. Это вызовет ошибки. Уберите его");
            }
            _name = name;
            _valueBoolDefolt = value;
            _valueBool = value;
            _type = StrategyParameterType.Bool;
            TabName = tabName;
        }

        /// <summary>
        /// blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// заглушка. нельзя создать переменную типа StrategyParameter с пустым конструктором
        /// </summary>
        private StrategyParameterBool()
        {

        }

        public string TabName
        {
            get; set;
        }

        /// <summary>
        /// to take a line to save
        /// взять строку для сохранения
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";
            save += _valueBool + "#";
            save += _valueBoolDefolt + "#";

            return save;
        }

        /// <summary>
        ///  download settings from the save file
        /// загрузить настройки из файла сохранения
        /// </summary>
        /// <param name="save"></param>
        public void LoadParamFromString(string[] save)
        {
            _name = save[0];
            _valueBool = Convert.ToBoolean(save[1]);

            try
            {
                _valueBoolDefolt = Convert.ToBoolean(save[2]);
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Parameter name. Used to identify a parameter in the settings windows
        /// Название параметра. Используется для идентификации параметра в окнах настроек
        /// </summary>
        public string Name
        {
            get { return _name; }
        }
        private string _name;

        /// <summary>
        /// parameter type
        /// тип параметра
        /// </summary>
        public StrategyParameterType Type
        {
            get { return _type; }
        }
        private StrategyParameterType _type;

        /// <summary>
        /// parameter Boolean value
        /// значение булева параметра
        /// </summary>
        public bool ValueBool
        {
            get
            {
                return _valueBool;
            }
            set
            {
                if (_valueBool == value)
                {
                    return;
                }
                _valueBool = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }
        private bool _valueBool;

        /// <summary>
        /// default setting for the parameter boolean
        /// значение по умолчанию для булева параметра
        /// </summary>
        public bool ValueBoolDefolt
        {
            get
            {
                return _valueBoolDefolt;
            }
        }
        private bool _valueBoolDefolt;

        /// <summary>
        /// event: the parameter has changed
        /// событие: параметр изменился
        /// </summary>
        public event Action ValueChange;
    }

    /// <summary>
    /// A strategy parameter that stores a collection of strings
    /// параметр стратегии хранящий в себе коллекцию строк
    /// </summary>
    public class StrategyParameterString : IIStrategyParameter
    {
        /// <summary>
        /// constructor to create a parameter storing variables of String type
        /// конструктор для создания параметра хранящего переменные типа String
        /// </summary>
        /// <param name="name">Parameter name/Имя параметра</param>
        /// <param name="value">Default value/Значение по умолчанию</param>
        /// <param name="collection">Possible value options/Возможные варианты значений</param>
        public StrategyParameterString(string name, string value, List<string> collection, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("название параметра у робота содержит спец-символ. Это вызовет ошибки. Уберите его");
            }
            bool isInArray = false;

            if (collection == null)
            {
                collection = new List<string>();
            }

            for (int i = 0; i < collection.Count; i++)
            {
                if (collection[i] == value)
                {
                    isInArray = true;
                    break;
                }
            }

            if (isInArray == false)
            {
                collection.Add(value);
            }

            _name = name;
            _valueString = value;
            _setStringValues = collection;
            _type = StrategyParameterType.String;
            TabName = tabName;
        }

        /// <summary>
        /// constructor to create a parameter storing variables of String type
        /// конструктор для создания параметра хранящего переменные типа String
        /// </summary>
        /// <param name="name">Parameter name/Имя параметра</param>
        /// <param name="value">Default value/Значение по умолчанию</param>
        public StrategyParameterString(string name, string value, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("название параметра у робота содержит спец-символ. Это вызовет ошибки. Уберите его");
            }
            if (value == null)
            {
                value = "";
            }

            _name = name;
            _valueString = value;
            _setStringValues = new List<string>() { value };
            _type = StrategyParameterType.String;
            TabName = tabName;
        }

        /// <summary>
        /// blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// заглушка. нельзя создать переменную типа StrategyParameter с пустым конструктором
        /// </summary>
        private StrategyParameterString()
        {

        }

        /// <summary>
        /// to take a line to save
        /// взять строку для сохранения
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";
            save += _valueString + "#";

            for (int i = 0; i < _setStringValues.Count; i++)
            {
                save += _setStringValues[i] + "#";
            }

            return save;
        }

        /// <summary>
        /// download settings from the save file
        /// загрузить настройки из файла сохранения
        /// </summary>
        /// <param name="save"></param>
        public void LoadParamFromString(string[] save)
        {
            _valueString = save[1];

            _setStringValues = new List<string>() { };

            for (int i = 2; i < save.Length; i++)
            {
                if (string.IsNullOrEmpty(save[i]))
                {
                    continue;
                }

                _setStringValues.Add(save[i]);
            }
        }

        /// <summary>
        /// Parameter name. Used to identify a parameter in the settings windows
        /// Название параметра. Используется для идентификации параметра в окнах настроек
        /// </summary>
        public string Name
        {
            get { return _name; }
        }
        private string _name;

        public string TabName
        {
            get; set;
        }


        /// <summary>
        /// parameter type
        /// тип параметра
        /// </summary>
        public StrategyParameterType Type
        {
            get { return _type; }
        }
        private StrategyParameterType _type;

        /// <summary>
        /// current value of the string type parameter
        /// текущее значение параметра типа string
        /// </summary>
        public string ValueString
        {
            get
            {
                return _valueString;
            }
            set
            {
                if (_valueString == value)
                {
                    return;
                }
                _valueString = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }
        private string _valueString;
        public List<string> ValuesString
        {
            get
            {
                if (_type != StrategyParameterType.String)
                {
                    throw new Exception("Попытка запросить у параметра с типом String, поле " + _type);
                }
                return _setStringValues;
            }
        }

        private List<string> _setStringValues;

        /// <summary>
        /// event: the parameter has changed
        /// событие: параметр изменился
        /// </summary>
        public event Action ValueChange;
    }

    public class StrategyParameterTimeOfDay : IIStrategyParameter
    {
        public StrategyParameterTimeOfDay(string name, int hour, int minute, int second, int millisecond, string tabName = null)
        {
            if (name.HaveExcessInString())
            {
                throw new Exception("название параметра у робота содержит спец-символ. Это вызовет ошибки. Уберите его");
            }
            _name = name;
            Value = new TimeOfDay();
            Value.Hour = hour;
            Value.Minute = minute;
            Value.Second = second;
            Value.Millisecond = millisecond;
            _type = StrategyParameterType.TimeOfDay;
            TabName = tabName;
        }

        public string Name
        {
            get { return _name; }
        }
        private string _name;

        public string TabName { get; set; }

        public TimeOfDay Value;

        public string GetStringToSave()
        {
            string save = _name + "#";
            save += Value.ToString() + "#";

            return save;
        }

        public void LoadParamFromString(string[] save)
        {
            if (Value.LoadFromString(save[1]) &&
                ValueChange != null)
            {
                ValueChange();
            }
        }

        public StrategyParameterType Type
        {
            get { return _type; }
        }
        private StrategyParameterType _type;

        public event Action ValueChange;

        public TimeSpan TimeSpan
        {
            get
            {
                return Value.TimeSpan;
            }
        }
    }

    public class TimeOfDay
    {
        public int Hour;

        public int Minute;

        public int Second;

        public int Millisecond;

        public override string ToString()
        {
            string result = Hour + ":";
            result += Minute + ":";
            result += Second + ":";
            result += Millisecond;

            return result;
        }

        public bool LoadFromString(string save)
        {
            string[] array = save.Split(':');

            bool paramUpdated = false;

            if (Hour != Convert.ToInt32(array[0]))
            {
                Hour = Convert.ToInt32(array[0]);
                paramUpdated = true;
            }
            if (Minute != Convert.ToInt32(array[1]))
            {
                Minute = Convert.ToInt32(array[1]);
                paramUpdated = true;
            }
            if (Second != Convert.ToInt32(array[2]))
            {
                Second = Convert.ToInt32(array[2]);
                paramUpdated = true;
            }
            if (Millisecond != Convert.ToInt32(array[3]))
            {
                Millisecond = Convert.ToInt32(array[3]);
                paramUpdated = true;
            }

            return paramUpdated;
        }

        public static bool operator >(TimeOfDay c1, DateTime c2)
        {
            if (c1.Hour > c2.Hour)
            {
                return true;
            }

            if (c1.Hour >= c2.Hour
                && c1.Minute > c2.Minute)
            {
                return true;
            }

            if (c1.Hour >= c2.Hour
                && c1.Minute >= c2.Minute
                && c1.Second > c2.Second)
            {
                return true;
            }

            if (c1.Hour >= c2.Hour
                && c1.Minute >= c2.Minute
                && c1.Second >= c2.Second
                && c1.Millisecond > c2.Millisecond)
            {
                return true;
            }

            return false;
        }

        public static bool operator <(TimeOfDay c1, DateTime c2)
        {
            if (c1.Hour < c2.Hour)
            {
                return true;
            }

            if (c1.Hour == c2.Hour
                && c1.Minute < c2.Minute)
            {
                return true;
            }

            if (c1.Hour == c2.Hour
                && c1.Minute == c2.Minute
                && c1.Second < c2.Second)
            {
                return true;
            }

            if (c1.Hour == c2.Hour
                && c1.Minute == c2.Minute
                && c1.Second == c2.Second
                && c1.Millisecond < c2.Millisecond)
            {
                return true;
            }

            return false;
        }

        public TimeSpan TimeSpan
        {
            get
            {
                TimeSpan time = new TimeSpan(0, Hour, Minute, Second);

                return time;
            }
        }
    }

    /// <summary>
    /// A strategy parameter to button click
    /// Параметр для обработки нажатия на кнопку
    /// </summary>
    public class StrategyParameterButton : IIStrategyParameter
    {
        public StrategyParameterButton(string buttonLabel, string tabName = null)
        {

            if (buttonLabel.HaveExcessInString())
            {
                throw new Exception("название параметра у робота содержит спец-символ. Это вызовет ошибки. Уберите его");
            }
            _name = buttonLabel;
            _type = StrategyParameterType.Button;
            TabName = tabName;
        }

        public string TabName { get; set; }

        /// <summary>
        /// blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// заглушка. нельзя создать переменную типа StrategyParameter с пустым конструктором
        /// </summary>
        private StrategyParameterButton()
        {

        }

        /// <summary>
        /// to take a line to save
        /// взять строку для сохранения
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";

            return save;
        }

        /// <summary>
        /// download settings from the save file
        /// загрузить настройки из файла сохранения
        /// </summary>
        /// <param name="save"></param>
        public void LoadParamFromString(string[] save)
        {
        }

        /// <summary>
        /// Parameter name. Used to identify a parameter in the settings windows
        /// Название параметра. Используется для идентификации параметра в окнах настроек
        /// </summary>
        public string Name
        {
            get { return _name; }
        }
        private string _name;

        /// <summary>
        /// parameter type
        /// тип параметра
        /// </summary>
        public StrategyParameterType Type
        {
            get { return _type; }
        }
        private StrategyParameterType _type;

        public event Action ValueChange;

        public void Click()
        {
            UserClickOnButtonEvent?.Invoke();
        }

        public event Action UserClickOnButtonEvent;
    }

    /// <summary>
    /// A strategy parameter to button click
    /// параметр стратегии типа CheckBox
    /// </summary>
    public class StrategyParameterCheckBox : IIStrategyParameter
    {
        public StrategyParameterCheckBox(string checkBoxLabel, bool isChecked, string tabName = null)
        {

            if (checkBoxLabel.HaveExcessInString())
            {
                throw new Exception("название параметра у робота содержит спец-символ. Это вызовет ошибки. Уберите его");
            }
            _name = checkBoxLabel;
            _type = StrategyParameterType.CheckBox;

            if (isChecked == true)
            {
                _checkState = CheckState.Checked;
            }
            else
            {
                _checkState = CheckState.Unchecked;
            }

            TabName = tabName;
        }

        public string TabName { get; set; }

        /// <summary>
        /// blank. it is impossible to create a variable of StrategyParameter type with an empty constructor
        /// заглушка. нельзя создать переменную типа StrategyParameter с пустым конструктором
        /// </summary>
        private StrategyParameterCheckBox()
        {

        }

        /// <summary>
        /// to take a line to save
        /// взять строку для сохранения
        /// </summary>
        public string GetStringToSave()
        {
            string save = _name + "#";

            if (_checkState == CheckState.Checked)
            {
                save += "true" + "#";
            }
            else
            {
                save += "false" + "#";
            }

            return save;
        }

        /// <summary>
        ///  download settings from the save file
        /// загрузить настройки из файла сохранения
        /// </summary>
        /// <param name="save"></param>
        public void LoadParamFromString(string[] save)
        {
            _name = save[0];

            try
            {
                if (save[1] == "true")
                {
                    _checkState = CheckState.Checked;
                }
                else
                {
                    _checkState = CheckState.Unchecked;
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Parameter name. Used to identify a parameter in the settings windows
        /// Название параметра. Используется для идентификации параметра в окнах настроек
        /// </summary>
        public string Name
        {
            get { return _name; }
        }
        private string _name;

        /// <summary>
        /// parameter state
        /// состояние параметра
        /// </summary>
        public CheckState CheckState
        {
            get
            {
                return _checkState;
            }
            set
            {
                if (_checkState == value)
                {
                    return;
                }
                _checkState = value;
                if (ValueChange != null)
                {
                    ValueChange();
                }
            }
        }

        private CheckState _checkState;

        /// <summary>
        /// parameter type
        /// тип параметра
        /// </summary>
        public StrategyParameterType Type
        {
            get { return _type; }
        }
        private StrategyParameterType _type;

        public event Action ValueChange;
    }
    /// <summary>
    /// parameter type
    /// тип параметра
    /// </summary>
    public enum StrategyParameterType
    {
        /// <summary>
        /// an integer number with the type Int
        /// целое число с типом Int
        /// </summary>
        Int,

        /// <summary>
        /// a floating point number of the decimal type
        /// число с плавающей точкой типа decimal
        /// </summary>
        Decimal,

        /// <summary>
        /// string
        /// строка
        /// </summary>
        String,

        /// <summary>
        /// Boolean value
        /// булево значение
        /// </summary>
        Bool,

        /// <summary>
        /// время
        /// </summary>
        TimeOfDay,

        /// <summary>
        /// нажатие на кнопку
        /// </summary>
        Button,

        /// <summary>
        /// надпись в окне параметров 
        /// </summary>
        Label,

        /// <summary>
        /// чекбокс в окне параметров 
        /// </summary>
        CheckBox
    }

}
