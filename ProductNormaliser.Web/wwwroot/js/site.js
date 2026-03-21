document.addEventListener("DOMContentLoaded", function () {
	document.querySelectorAll("[data-category-selector]").forEach(function (selectorRoot) {
		var summaryRoot = selectorRoot.querySelector("[data-category-summary]");
		var launchButton = selectorRoot.closest("form")?.querySelector("[data-category-launch]");

		function getCheckboxes() {
			return Array.from(selectorRoot.querySelectorAll("[data-category-checkbox]"));
		}

		function getSelectedCheckboxes() {
			return getCheckboxes().filter(function (checkbox) {
				return checkbox.checked && !checkbox.disabled;
			});
		}

		function updateFamilyButtons() {
			selectorRoot.querySelectorAll("[data-family-toggle]").forEach(function (button) {
				var familyKey = button.getAttribute("data-family-key");
				var familyCheckboxes = getCheckboxes().filter(function (checkbox) {
					return checkbox.closest("[data-family-key]")?.getAttribute("data-family-key") === familyKey && !checkbox.disabled;
				});
				var allSelected = familyCheckboxes.length > 0 && familyCheckboxes.every(function (checkbox) { return checkbox.checked; });
				button.textContent = allSelected ? button.getAttribute("data-clear-label") : button.getAttribute("data-select-label");
			});
		}

		function updateCardState() {
			getCheckboxes().forEach(function (checkbox) {
				var card = checkbox.closest("[data-category-card]");
				if (!card) {
					return;
				}

				card.classList.toggle("selected", checkbox.checked);
			});
		}

		function updateSummary() {
			if (!summaryRoot) {
				return;
			}

			var selected = getSelectedCheckboxes();
			var uniqueFamilies = new Set();
			var totalScore = 0;

			selected.forEach(function (checkbox) {
				uniqueFamilies.add(checkbox.getAttribute("data-family-display-name") || "");
				totalScore += Number(checkbox.getAttribute("data-score") || "0");
			});

			var selectedCount = selected.length;
			var averageScore = selectedCount === 0 ? 0 : (totalScore / selectedCount) * 100;

			var countNode = summaryRoot.querySelector("[data-selected-count]");
			var familyCountNode = summaryRoot.querySelector("[data-selected-family-count]");
			var averageNode = summaryRoot.querySelector("[data-selected-average]");
			var enabledNode = summaryRoot.querySelector("[data-selected-enabled]");
			var emptyNode = summaryRoot.querySelector("[data-selected-empty]");
			var listNode = summaryRoot.querySelector("[data-selected-list]");

			if (countNode) {
				countNode.textContent = String(selectedCount);
			}

			if (familyCountNode) {
				familyCountNode.textContent = String(uniqueFamilies.size);
			}

			if (averageNode) {
				averageNode.textContent = averageScore.toFixed(1) + "%";
			}

			if (enabledNode) {
				enabledNode.textContent = String(selectedCount);
			}

			if (emptyNode) {
				emptyNode.hidden = selectedCount > 0;
			}

			if (listNode) {
				listNode.innerHTML = "";
				selected.forEach(function (checkbox) {
					var chip = document.createElement("span");
					chip.className = "chip";
					chip.setAttribute("data-selected-chip", "true");
					chip.textContent = checkbox.getAttribute("data-display-name") || checkbox.value;
					listNode.appendChild(chip);
				});
			}

			if (launchButton) {
				launchButton.disabled = selectedCount === 0;
			}
		}

		function syncUi() {
			updateCardState();
			updateFamilyButtons();
			updateSummary();
		}

		selectorRoot.querySelectorAll("[data-family-toggle]").forEach(function (button) {
			button.addEventListener("click", function () {
				var familyKey = button.getAttribute("data-family-key");
				var familyCheckboxes = getCheckboxes().filter(function (checkbox) {
					return checkbox.closest("[data-family-key]")?.getAttribute("data-family-key") === familyKey && !checkbox.disabled;
				});
				var shouldSelect = familyCheckboxes.some(function (checkbox) { return !checkbox.checked; });
				familyCheckboxes.forEach(function (checkbox) {
					checkbox.checked = shouldSelect;
				});
				syncUi();
			});
		});

		getCheckboxes().forEach(function (checkbox) {
			checkbox.addEventListener("change", syncUi);
		});

		syncUi();
	});
});
