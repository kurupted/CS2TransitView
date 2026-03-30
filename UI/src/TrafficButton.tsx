import { trigger } from "cs2/api";
import { useValue } from "cs2/api";
import { Button, Tooltip, Portal } from "cs2/ui";
import { toolActive } from "./bindings";
import { transitOpen } from "./bindings";
import TransitIcon from "./TransitIcon.svg";

export const TrafficButton = () => {
    console.log("[BetterTransitView] TrafficButton rendering...");

    try {
        const active = useValue(toolActive);
        const transitPanelOpen = useValue(transitOpen);

        return (
            <>
                <Tooltip tooltip="Better Transit View">
                    <Button
                        src="coui://uil/Standard/GenericVehicles.svg"
                        selected={active}
                        variant="floating"
                        onSelect={() => {
                            trigger("BetterTransitView", "setToolActive", !active);
                        }}
                    />
                </Tooltip>

                <Tooltip tooltip="Transit Overview">
                    <Button
                        src={TransitIcon}
                        selected={transitPanelOpen}
                        variant="floating"
                        onSelect={() => trigger("BetterTransitView", "toggleTransitCustom", !transitPanelOpen)}
                    />
                </Tooltip>

                {/* Portal overlay */}
                {active && (
                    <Portal>
                        <div style={{
                            position: "absolute",
                            top: "150rem",
                            left: "50%",
                            transform: "translateX(-50%)",
                            padding: "10rem 20rem",
                            background: "rgba(0, 0, 0, 0.8)",
                            color: "white",
                            borderRadius: "5rem",
                            fontSize: "16rem",
                            pointerEvents: "none",
                            zIndex: 10000
                        }}>
                            Pick a road segment, path, or transit structure.
                        </div>
                    </Portal>
                )}
            </>
        );
    } catch (error) {
        console.error("[BetterTransitView] Button render error:", error);
        return null;
    }
};

export default TrafficButton;