using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

namespace AegisDrive.Infrastructure.Services.Notification.Templates;

public static class EmailTemplates
{
    public const string CriticalAlertTemplateName = "AegisDrive_CriticalAlert_v3"; // Updated Version
    public const string GeneralNotificationTemplateName = "AegisDrive_General_v1";
    public const string HighAlertTemplateName = "AegisDrive_HighAlert_v1";

    public static async Task InitializeTemplates(IAmazonSimpleEmailService emailService)
    {
        // 1. CRITICAL ALERT TEMPLATE (Matched to your Service)
        var criticalTemplate = new CreateTemplateRequest
        {
            Template = new Template
            {
                TemplateName = CriticalAlertTemplateName,
                SubjectPart = "🚨 CRITICAL: {{EventType}} - {{DriverName}} ({{VehiclePlate}})",
                HtmlPart = """
                    <div style="font-family: 'Segoe UI', Arial, sans-serif; max-width: 650px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden; background-color: #ffffff;">
                        
                        <div style="background-color: #d32f2f; color: white; padding: 25px; text-align: center;">
                            <h1 style="margin: 0; font-size: 24px; text-transform: uppercase;">CRITICAL SAFETY ALERT</h1>
                            <p style="margin: 5px 0 0; font-size: 16px; opacity: 0.9;">{{EventType}} Detected • Immediate Action Required</p>
                        </div>
                        
                        <div style="padding: 30px;">
                            
                            <div style="display: flex; align-items: center; margin-bottom: 25px; padding-bottom: 20px; border-bottom: 1px solid #eee;">
                                <img src="{{DriverProfilePicUrl}}" alt="Profile" style="width: 80px; height: 80px; border-radius: 50%; object-fit: cover; border: 3px solid #f0f0f0; margin-right: 20px;" />
                                <div>
                                    <h2 style="margin: 0; color: #333; font-size: 20px;">{{DriverName}}</h2>
                                    <p style="margin: 5px 0 0; color: #666;">Vehicle: <strong>{{VehiclePlate}}</strong></p>
                                    </div>
                            </div>

                            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin-bottom: 25px;">
                                <div style="background-color: #fff5f5; padding: 15px; border-radius: 6px; border-left: 4px solid #d32f2f;">
                                    <p style="margin: 0; font-size: 12px; color: #d32f2f; font-weight: bold;">INCIDENT TYPE</p>
                                    <p style="margin: 5px 0 0; font-size: 16px; font-weight: bold; color: #333;">{{Message}}</p>
                                </div>
                                <div style="background-color: #f8f9fa; padding: 15px; border-radius: 6px; border-left: 4px solid #2c3e50;">
                                    <p style="margin: 0; font-size: 12px; color: #2c3e50; font-weight: bold;">TIME & SPEED</p>
                                    <p style="margin: 5px 0 0; font-size: 16px; color: #333;">{{Timestamp}} <br/> <span style="font-size: 14px; color: #666;">@ {{Speed}} km/h</span></p>
                                </div>
                            </div>

                            <div style="margin-bottom: 30px;">
                                <p style="margin: 0 0 10px; font-weight: bold; color: #555;">📍 Incident Location</p>
                                <a href="{{MapLink}}" style="display: block; background-color: #e3f2fd; color: #0277bd; padding: 15px; text-decoration: none; border-radius: 6px; text-align: center; font-weight: bold; border: 1px dashed #0277bd;">
                                    Open GPS Location in Maps ↗
                                </a>
                            </div>

                            <h3 style="margin: 0 0 15px; color: #333; border-top: 1px solid #eee; padding-top: 20px;">📸 Incident Evidence</h3>
                            <div style="display: flex; gap: 15px;">
                                <div style="flex: 1;">
                                    <p style="margin: 0 0 8px; font-size: 12px; font-weight: bold; color: #666; text-transform: uppercase;">Driver State</p>
                                    <div style="position: relative; padding-bottom: 75%; height: 0; overflow: hidden; border-radius: 6px; border: 1px solid #ddd;">
                                        <img src="{{DriverImageUrl}}" alt="Driver Face" style="position: absolute; top: 0; left: 0; width: 100%; height: 100%; object-fit: cover;" />
                                    </div>
                                </div>
                                <div style="flex: 1;">
                                    <p style="margin: 0 0 8px; font-size: 12px; font-weight: bold; color: #666; text-transform: uppercase;">Road Context</p>
                                    <div style="position: relative; padding-bottom: 75%; height: 0; overflow: hidden; border-radius: 6px; border: 1px solid #ddd;">
                                        <img src="{{RoadImageUrl}}" alt="Road View" style="position: absolute; top: 0; left: 0; width: 100%; height: 100%; object-fit: cover;" />
                                    </div>
                                </div>
                            </div>

                            <div style="margin-top: 30px; text-align: center;">
                                <a href="https://dashboard.aegisdrive.com/incidents/{{EventId}}" 
                                   style="background-color: #d32f2f; color: white; padding: 12px 30px; text-decoration: none; border-radius: 25px; font-weight: bold; display: inline-block;">
                                    View Full Report
                                </a>
                            </div>
                        </div>
                        
                        <div style="background-color: #f8f9fa; padding: 15px; text-align: center; border-top: 1px solid #eee; font-size: 12px; color: #999;">
                            <p style="margin: 0;">AegisDrive Automated Safety System</p>
                            <p style="margin: 5px 0 0;">Vehicle ID: {{VehiclePlate}} • Device: {{DeviceId}}</p>
                        </div>
                    </div>
                    """,
                TextPart = """
                    CRITICAL SAFETY ALERT
                    =====================
                    Driver: {{DriverName}}
                    Vehicle: {{VehiclePlate}}
                    Issue: {{Message}}
                    
                    Status: {{EventType}}
                    Speed: {{Speed}} km/h
                    Time: {{Timestamp}}
                    
                    Location Link: {{MapLink}}
                    
                    EVIDENCE:
                    Driver Image: {{DriverImageUrl}}
                    Road Image: {{RoadImageUrl}}
                    
                    Login to dashboard for full details.
                    """
            }
        };


        // 3. HIGH ALERT TEMPLATE (No Images, Action Button)
        var highTemplate = new CreateTemplateRequest
        {
            Template = new Template
            {
                TemplateName = "AegisDrive_HighAlert_v1",
                SubjectPart = "⚠️ WARNING: {{EventType}} - {{DriverName}}",
                HtmlPart = """
            <div style="font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden; background-color: #ffffff;">
                
                <div style="background-color: #f57c00; color: white; padding: 20px; text-align: center;">
                    <h1 style="margin: 0; font-size: 22px; text-transform: uppercase;">SAFETY WARNING</h1>
                    <p style="margin: 5px 0 0; font-size: 16px; opacity: 0.9;">Driver Behavior Alert</p>
                </div>
                
                <div style="padding: 30px;">
                    
                    <p style="font-size: 16px; color: #333;"><strong>Attention Fleet Manager,</strong></p>
                    <p style="color: #555; line-height: 1.5;">
                        A safety warning has been triggered for one of your vehicles. The driver was detected engaging in risky behavior while a road hazard was present.
                    </p>

                    <div style="background-color: #fff3e0; border-left: 5px solid #f57c00; padding: 15px; margin: 20px 0; border-radius: 4px;">
                        <p style="margin: 5px 0;"><strong>Driver:</strong> {{DriverName}}</p>
                        <p style="margin: 5px 0;"><strong>Vehicle:</strong> {{VehiclePlate}}</p>
                        <p style="margin: 5px 0;"><strong>Issue:</strong> {{Message}}</p>
                        <p style="margin: 5px 0;"><strong>Time:</strong> {{Timestamp}}</p>
                    </div>

                    <div style="text-align: center; margin-top: 30px; margin-bottom: 20px;">
                        <a href="https://dashboard.aegisdrive.com/incidents/{{EventId}}" 
                           style="background-color: #f57c00; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; font-weight: bold; font-size: 16px; display: inline-block;">
                            View Incident Details & Evidence
                        </a>
                        <p style="margin-top: 15px; font-size: 13px; color: #777;">
                            Click to view driver snapshots, road context, and location history.
                        </p>
                    </div>

                </div>
                
                <div style="background-color: #f8f9fa; padding: 15px; text-align: center; border-top: 1px solid #eee; font-size: 12px; color: #999;">
                    <p style="margin: 0;">AegisDrive Fleet Safety</p>
                    <p style="margin: 5px 0 0;">Device: {{DeviceId}}</p>
                </div>
            </div>
            """,
                TextPart = """
            SAFETY WARNING
            ==============
            Driver: {{DriverName}}
            Vehicle: {{VehiclePlate}}
            Issue: {{Message}}
            Time: {{Timestamp}}

            View full details and evidence here:
            https://dashboard.aegisdrive.com/incidents/{{EventId}}
            """
            }
        };


        // 2. GENERAL NOTIFICATION TEMPLATE
        var generalTemplate = new CreateTemplateRequest
        {
            Template = new Template
            {
                TemplateName = GeneralNotificationTemplateName,
                SubjectPart = "AegisDrive Notification: {{Subject}}",
                HtmlPart = """
                    <div style="font-family: Arial, sans-serif; padding: 20px; max-width: 600px; margin: 0 auto; border: 1px solid #eee;">
                        <h2 style="color: #2c3e50; margin-top: 0;">{{Subject}}</h2>
                        <p style="color: #555; line-height: 1.6;">{{Body}}</p>
                        <hr style="border: 0; border-top: 1px solid #eee; margin: 20px 0;" />
                        <p style="font-size: 12px; color: #7f8c8d;">AegisDrive Fleet Safety System</p>
                    </div>
                    """,
                TextPart = "{{Body}}"
            }
        };



        await SafeUpdateTemplate(emailService, criticalTemplate);
        await SafeUpdateTemplate(emailService, highTemplate);
        await SafeUpdateTemplate(emailService, generalTemplate);
    }

    private static async Task SafeUpdateTemplate(IAmazonSimpleEmailService client, CreateTemplateRequest request)
    {
        try
        {
            await client.DeleteTemplateAsync(new DeleteTemplateRequest { TemplateName = request.Template.TemplateName });
        }
        catch { /* Ignore if doesn't exist */ }

        await client.CreateTemplateAsync(request);
    }
}