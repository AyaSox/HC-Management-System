// Global UI enhancements shared across forms
(function(){
  document.addEventListener('DOMContentLoaded', function() {
    // Initialize Select2 on searchable selects if available
    try {
      if (window.jQuery && typeof window.jQuery.fn.select2 === 'function') {
        window.jQuery('.searchable-select').select2({
          placeholder: 'Search and select...',
          allowClear: true,
          width: '100%'
        });
      }
    } catch (e) {
      console.warn('Select2 init skipped:', e);
    }

    // Salary sanity hint (used on Create/Edit)
    const salaryInput = document.querySelector('input[name="Salary"]');
    if (salaryInput) {
      salaryInput.addEventListener('blur', function() {
        const v = parseFloat(this.value);
        if (v && v < 1000) {
          alert('Salary seems unusually low. Please verify the amount.');
        }
      });
    }
  });
})();
