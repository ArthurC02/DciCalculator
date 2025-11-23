using DciCalculator.Core.Interfaces;

namespace DciCalculator.DayCount;

/// <summary>
/// Day Count Calculator 工廠
/// 根據慣例類型返回對應的計算器實例
/// </summary>
public sealed class DayCountCalculatorFactory
{
    private readonly Dictionary<DayCountConvention, IDayCountCalculator> _calculators;

    /// <summary>
    /// 建立工廠並初始化所有計算器
    /// </summary>
    public DayCountCalculatorFactory()
    {
        _calculators = new Dictionary<DayCountConvention, IDayCountCalculator>
        {
            [DayCountConvention.Act365] = new Act365Calculator(),
            [DayCountConvention.Act360] = new Act360Calculator(),
            [DayCountConvention.ActAct] = new ActActCalculator(),
            [DayCountConvention.Thirty360] = new Thirty360Calculator(),
            [DayCountConvention.Bus252] = new Bus252Calculator()
        };
    }

    /// <summary>
    /// 根據慣例取得計算器
    /// </summary>
    public IDayCountCalculator GetCalculator(DayCountConvention convention)
    {
        if (_calculators.TryGetValue(convention, out var calculator))
            return calculator;

        throw new ArgumentOutOfRangeException(
            nameof(convention),
            convention,
            $"不支援的日數計算慣例: {convention}");
    }

    /// <summary>
    /// 取得所有支援的慣例
    /// </summary>
    public IEnumerable<DayCountConvention> GetSupportedConventions()
    {
        return _calculators.Keys;
    }
}
