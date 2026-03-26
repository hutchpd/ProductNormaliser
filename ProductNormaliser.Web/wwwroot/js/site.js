document.addEventListener("DOMContentLoaded", function () {
	var navigationDrawer = document.getElementById("appNavigationDrawer");
	if (navigationDrawer && window.bootstrap && window.bootstrap.Offcanvas) {
		var navigationDrawerInstance = window.bootstrap.Offcanvas.getOrCreateInstance(navigationDrawer);
		navigationDrawer.querySelectorAll("[data-nav-drawer-dismiss]").forEach(function (link) {
			link.addEventListener("click", function () {
				navigationDrawerInstance.hide();
			});
		});
	}

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

		function updateSourceFiltering() {
			var selectedCategories = new Set(getSelectedCheckboxes().map(function (checkbox) {
				return checkbox.value.toLowerCase();
			}));

			Array.from(selectorRoot.closest("form")?.querySelectorAll("[data-source-checkbox]") || []).forEach(function (sourceCheckbox) {
				var sourceCard = sourceCheckbox.closest("[data-source-card]");
				var isBaseEnabled = sourceCheckbox.getAttribute("data-source-enabled") === "true";
				var supportedCategories = (sourceCheckbox.getAttribute("data-supported-categories") || "")
					.split(",")
					.map(function (value) { return value.trim().toLowerCase(); })
					.filter(function (value) { return value.length > 0; });

				var matchesSelection = selectedCategories.size === 0 || supportedCategories.some(function (categoryKey) {
					return selectedCategories.has(categoryKey);
				});

				var isEnabled = isBaseEnabled && matchesSelection;
				sourceCheckbox.disabled = !isEnabled;
				if (!isEnabled) {
					sourceCheckbox.checked = false;
				}

				if (sourceCard) {
					sourceCard.classList.toggle("disabled", !isEnabled);
				}
			});
		}

		function syncUi() {
			updateCardState();
			updateFamilyButtons();
			updateSummary();
			updateSourceFiltering();
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

	document.querySelectorAll("[data-schema-toggle-form]").forEach(function (form) {
		var toggleUrl = form.getAttribute("data-schema-toggle-url");
		var categoryKeyInput = form.querySelector("input[name='CategorySchema.CategoryKey']");
		var feedbackNode = form.querySelector("[data-schema-toggle-feedback]");

		function setFeedback(message, state) {
			if (!feedbackNode) {
				return;
			}

			feedbackNode.textContent = message || "";
			if (state) {
				feedbackNode.setAttribute("data-state", state);
			} else {
				feedbackNode.removeAttribute("data-state");
			}
		}

		function updateRequiredLabel(checkbox) {
			var label = checkbox.closest("label")?.querySelector("[data-required-label]");
			if (label) {
				label.textContent = checkbox.checked ? "required" : "optional";
			}
		}

		form.querySelectorAll("[data-schema-required-toggle]").forEach(function (checkbox) {
			checkbox.addEventListener("change", async function () {
				var previousChecked = !checkbox.checked;
				var attributeKey = checkbox.getAttribute("data-attribute-key") || "";

				updateRequiredLabel(checkbox);
				setFeedback("Saving schema update...", "saving");
				checkbox.disabled = true;

				try {
					var formData = new FormData(form);
					formData.set("categoryKey", categoryKeyInput?.value || "");
					formData.set("attributeKey", attributeKey);
					formData.set("isRequired", checkbox.checked ? "true" : "false");

					var response = await fetch(toggleUrl || window.location.pathname + "?handler=ToggleCategorySchemaRequired", {
						method: "POST",
						body: formData,
						headers: {
							"X-Requested-With": "XMLHttpRequest"
						}
					});

					var payload = null;
					try {
						payload = await response.json();
					} catch (error) {
						payload = null;
					}

					if (!response.ok) {
						throw new Error(payload?.message || "The schema update could not be saved.");
					}

					setFeedback(payload?.message || "Schema updated.", "success");
				} catch (error) {
					checkbox.checked = previousChecked;
					updateRequiredLabel(checkbox);
					setFeedback(error instanceof Error ? error.message : "The schema update could not be saved.", "error");
				} finally {
					checkbox.disabled = false;
				}
			});

			updateRequiredLabel(checkbox);
		});
	});
});
