class LoadingStateManager {
    static showLoading(element, message = 'Loading...') {
        const loadingHtml = `
            <div class="d-flex justify-content-center align-items-center p-3">
                <div class="spinner-border spinner-border-sm me-2" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
                <span>${message}</span>
            </div>
        `;
        
        if (typeof element === 'string') {
            element = document.querySelector(element);
        }
        
        if (element) {
            element.innerHTML = loadingHtml;
        }
    }

    static hideLoading(element, originalContent = '') {
        if (typeof element === 'string') {
            element = document.querySelector(element);
        }
        
        if (element) {
            element.innerHTML = originalContent;
        }
    }

    static showButtonLoading(button, loadingText = 'Processing...') {
        if (typeof button === 'string') {
            button = document.querySelector(button);
        }
        
        if (button) {
            button.dataset.originalText = button.innerHTML;
            button.innerHTML = `
                <span class="spinner-border spinner-border-sm me-2" role="status"></span>
                ${loadingText}
            `;
            button.disabled = true;
        }
    }

    static hideButtonLoading(button) {
        if (typeof button === 'string') {
            button = document.querySelector(button);
        }
        
        if (button && button.dataset.originalText) {
            button.innerHTML = button.dataset.originalText;
            button.disabled = false;
            delete button.dataset.originalText;
        }
    }

    static showFormLoading(formId) {
        const form = document.getElementById(formId);
        if (form) {
            const buttons = form.querySelectorAll('button[type="submit"]');
            buttons.forEach(button => {
                this.showButtonLoading(button, 'Saving...');
            });
        }
    }

    static hideFormLoading(formId) {
        const form = document.getElementById(formId);
        if (form) {
            const buttons = form.querySelectorAll('button[type="submit"]');
            buttons.forEach(button => {
                this.hideButtonLoading(button);
            });
        }
    }
}

// Enhanced form submission with loading states
function submitFormWithLoading(formId, buttonId) {
    const form = document.getElementById(formId);
    const button = document.getElementById(buttonId);
    
    if (form && button) {
        LoadingStateManager.showButtonLoading(button, 'Saving...');
        
        // Add form validation
        if (!form.checkValidity()) {
            LoadingStateManager.hideButtonLoading(button);
            form.reportValidity();
            return false;
        }
        
        // Simulate form processing time or actual AJAX call
        setTimeout(() => {
            form.submit();
        }, 100);
    }
    return true;
}

// Auto-attach to forms with loading class
document.addEventListener('DOMContentLoaded', function() {
    const formsWithLoading = document.querySelectorAll('form.loading-enabled');
    formsWithLoading.forEach(form => {
        form.addEventListener('submit', function(e) {
            const submitButton = form.querySelector('button[type="submit"]');
            if (submitButton) {
                LoadingStateManager.showButtonLoading(submitButton, 'Processing...');
            }
        });
    });
});