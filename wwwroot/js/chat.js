// Function to scroll to the bottom of the chat container
function scrollToBottom(elementId) {
    var element = document.getElementById(elementId);
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}

// Function to auto-resize textarea as user types
function autoResizeTextarea(elementId) {
    var textarea = document.getElementById(elementId);
    if (textarea) {
        textarea.style.height = 'auto';
        textarea.style.height = (textarea.scrollHeight) + 'px';
        
        // Handle overflow based on height
        if (textarea.scrollHeight > 200) {
            textarea.style.overflowY = 'auto';
        } else {
            textarea.style.overflowY = 'hidden';
        }
    }
}

// Initialize chat UI functionality
document.addEventListener('DOMContentLoaded', function() {
    // Auto-focus the input field
    const chatInput = document.getElementById('chatInput');
    if (chatInput) {
        chatInput.focus();
        
        // Setup autosize for the textarea
        chatInput.addEventListener('input', function() {
            autoResizeTextarea('chatInput');
        });
    }
    
    // Scroll to bottom on initial load
    scrollToBottom('messageContainer');
}); 