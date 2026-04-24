
    using System;
    using Sigtrap.Relays;

    public class UnitDataModule
    {
        protected UnitData _unitData;
        protected Relay _onCalculate = new Relay();

        public void Initialize(UnitData unitData, Action onCalculate)
        {
            _unitData = unitData;
            _onCalculate.AddListener(onCalculate);
        }
    }
