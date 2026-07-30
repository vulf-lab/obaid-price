using CommunityToolkit.Mvvm.ComponentModel;

namespace CostWise.App.ViewModels;

public partial class PricingViewModel : ObservableObject
{
    public ActivePricingViewModel ActivePricing { get; }
    public CostingViewModel CustomizedPricing { get; }

    public PricingViewModel(ActivePricingViewModel activePricing, CostingViewModel customizedPricing)
    {
        ActivePricing = activePricing;
        CustomizedPricing = customizedPricing;
    }
}
