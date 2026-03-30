import { trigger } from "cs2/api";
import { useValue } from "cs2/api";
import { Button, Tooltip } from "cs2/ui";
import { transitOpen } from "./bindings";
import TransitIcon from "./TransitIcon.svg";

export const TransitButton = () => {
    console.log("[BetterTransitView] Button rendering...");

    try {
        const transitPanelOpen = useValue(transitOpen);

        return (
            <>
                <Tooltip tooltip="Transit Overview">
                    <Button
                        src={TransitIcon}
                        selected={transitPanelOpen}
                        variant="floating"
                        onSelect={() => trigger("BetterTransitView", "toggleTransitCustom", !transitPanelOpen)}
                    />
                </Tooltip>
            </>
        );
    } catch (error) {
        console.error("[BetterTransitView] Button render error:", error);
        return null;
    }
};

export default TransitButton;