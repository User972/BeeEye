"""BeeEye platform ML and statistical workloads.

Subpackages map to the use-case model families:

* ``common``            — shared metrics, evaluation and utilities
* ``forecasting``       — UC2 forecast accuracy + baselines
* ``inventory_aging``   — UC5 explainable aging-risk scoring
* ``order_optimisation``— UC1 order-quantity optimisation
* ``procurement``       — UC4 procurement quantity + safety stock
* ``after_sales``       — UC6 sales/after-sales correlation
* ``spare_parts``       — UC7 intermittent spare-parts demand

The seed modules here are pure-standard-library so they import and test without
the heavy ML stack declared in ``pyproject.toml``. They also mirror the
deterministic logic in the wireframe ``engine.js`` so the port is traceable.
"""

__version__ = "0.1.0"
