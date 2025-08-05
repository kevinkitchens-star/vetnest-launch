let customAmount = 10.00;

// Listen for changes to the donation amount
document.getElementById("donationAmount").addEventListener("input", function(e) {
    customAmount = parseFloat(e.target.value) || 0;
});

// Render the PayPal donation button
paypal.Buttons({
    style: {
        color: 'gold',
        shape: 'pill',
        label: 'donate'
    },
    createOrder: function(data, actions) {
        return actions.order.create({
            purchase_units: [{
                amount: {
                    value: customAmount.toFixed(2)
                }
            }]
        });
    },
    onApprove: function(data, actions) {
        return actions.order.capture().then(function(details) {
            alert(`Thank you, ${details.payer.name.given_name}! Your donation of $${customAmount.toFixed(2)} has been received.`);
        });
    },
    onError: function(err) {
        console.error('PayPal Error:', err);
        alert('Something went wrong during checkout. Please try again.');
    }
}).render('#paypal-button-container');
